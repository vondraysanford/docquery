#!/usr/bin/env python3
"""DocQuery provider benchmark — fills the README's Phase 2 table with
measured numbers. Python stdlib only; drives the running API over HTTP.

Usage (API must already be running at --api in the matching provider mode):

  # Local pass (Mac Ollama, llama3 8B):
  python3 benchmarks/run_benchmark.py --mode local --label "Local (Llama 3 8B)"

  # Azure pass (start the API with DocQuery__Provider=Azure):
  python3 benchmarks/run_benchmark.py --mode azure --label "Azure (gpt-5-mini)"

  # DGX Spark pass over an SSH tunnel (ssh -N -L 11435:localhost:11434 user@spark),
  # API started with DocQuery__Ollama__BaseUrl=http://localhost:11435 and
  # DocQuery__Ollama__ChatModel=llama3:70b:
  python3 benchmarks/run_benchmark.py --mode local --label "Local (Llama 3 70B)" \\
      --ollama-url http://localhost:11435 --chat-model llama3:70b

Each pass ingests docs/samples/ through the real API (timed), runs the
question set through /api/query (timed), measures raw engine tok/s, cleans
up its uploads, merges results into benchmarks/results.json, and saves the
answers for subjective quality review.
"""

import argparse
import json
import statistics
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SAMPLES = sorted((REPO / "docs" / "samples").glob("*.md"))
QUESTIONS = json.loads((REPO / "benchmarks" / "questions.json").read_text())
RESULTS_PATH = REPO / "benchmarks" / "results.json"

TOKS_PROMPT = "Explain, in about 200 words, how retrieval-augmented generation works."

# gpt-5-mini list prices, USD per 1M tokens (checked 2026-07; re-verify before
# publishing — the cost row should also be cross-checked against Azure Cost
# Management actuals).
AZURE_INPUT_PER_M = 0.25
AZURE_OUTPUT_PER_M = 2.00


def http_json(url, payload=None, headers=None, method=None, timeout=600):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(url, data=data, method=method or ("POST" if data else "GET"))
    req.add_header("Content-Type", "application/json")
    for key, value in (headers or {}).items():
        req.add_header(key, value)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = resp.read()
        return json.loads(body) if body else None


def upload_document(api, path):
    boundary = "docquerybenchmark"
    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="file"; filename="{path.name}"\r\n'
        f"Content-Type: text/markdown\r\n\r\n"
        f"{path.read_text()}\r\n--{boundary}--\r\n"
    ).encode()
    req = urllib.request.Request(f"{api}/api/documents/upload", data=body, method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    with urllib.request.urlopen(req, timeout=300) as resp:
        return json.loads(resp.read())


def measure_ingestion(api):
    print(f"Ingesting {len(SAMPLES)} sample documents...")
    started = time.monotonic()
    doc_ids = [upload_document(api, path)["documentId"] for path in SAMPLES]
    elapsed = time.monotonic() - started
    docs_per_min = len(SAMPLES) / elapsed * 60
    print(f"  {elapsed:.1f}s -> {docs_per_min:.1f} docs/min")
    return doc_ids, docs_per_min


def measure_queries(api):
    print(f"Running {len(QUESTIONS)} questions x 2 passes...")
    latencies, answers = [], []
    for round_number in range(2):
        for question in QUESTIONS:
            started = time.monotonic()
            result = http_json(f"{api}/api/query", {"question": question}, timeout=600)
            latencies.append(time.monotonic() - started)
            if round_number == 0:
                answers.append({"question": question, "answer": result["answer"],
                                "sources": len(result["sources"])})
    mean = statistics.mean(latencies)
    p95 = sorted(latencies)[int(len(latencies) * 0.95) - 1]
    print(f"  mean {mean:.2f}s, p95 {p95:.2f}s")
    return mean, p95, answers


def measure_ollama_toks(ollama_url, model):
    print(f"Measuring tok/s at {ollama_url} ({model}), 3 runs...")
    rates = []
    for _ in range(3):
        result = http_json(f"{ollama_url}/api/generate",
                           {"model": model, "prompt": TOKS_PROMPT, "stream": False})
        rates.append(result["eval_count"] / (result["eval_duration"] / 1e9))
    rate = statistics.mean(rates)
    print(f"  {rate:.1f} tok/s")
    return rate, None


def measure_azure_toks():
    config = json.loads((REPO / "src" / "DocQuery.Api" / "appsettings.json").read_text())
    azure = config["DocQuery"]["Azure"]["OpenAI"]
    endpoint = azure["Endpoint"].rstrip("/")
    deployment = azure["ChatDeployment"]
    url = f"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-10-21"

    # RAG-shaped prompt (system template + full sample corpus as context) so
    # the measured token usage reflects what real queries cost.
    context = "\n\n".join(path.read_text() for path in SAMPLES)
    print(f"Measuring tok/s at Azure ({deployment}), 3 runs...")
    rates, prompt_tokens, completion_tokens = [], [], []
    for _ in range(3):
        started = time.monotonic()
        result = http_json(url, {
            "messages": [
                {"role": "system", "content": f"Answer only from this context:\n{context}"},
                {"role": "user", "content": TOKS_PROMPT},
            ],
        }, headers={"api-key": azure["ApiKey"]})
        elapsed = time.monotonic() - started
        usage = result["usage"]
        rates.append(usage["completion_tokens"] / elapsed)
        prompt_tokens.append(usage["prompt_tokens"])
        completion_tokens.append(usage["completion_tokens"])

    rate = statistics.mean(rates)
    cost_per_1k = (statistics.mean(prompt_tokens) * AZURE_INPUT_PER_M
                   + statistics.mean(completion_tokens) * AZURE_OUTPUT_PER_M) / 1e6 * 1000
    print(f"  {rate:.1f} tok/s; est ${cost_per_1k:.2f} per 1K queries")
    return rate, cost_per_1k


def cleanup(api, doc_ids):
    for doc_id in doc_ids:
        http_json(f"{api}/api/documents/{doc_id}", method="DELETE")
    print(f"Cleaned up {len(doc_ids)} benchmark documents.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["local", "azure"], required=True)
    parser.add_argument("--label", required=True, help="README column label")
    parser.add_argument("--api", default="http://localhost:5000")
    parser.add_argument("--ollama-url", default="http://localhost:11434")
    parser.add_argument("--chat-model", default="llama3")
    args = parser.parse_args()

    http_json(f"{args.api}/health")  # fail fast if the API isn't up

    doc_ids, docs_per_min = measure_ingestion(args.api)
    try:
        time.sleep(3)  # Azure AI Search indexing is near-real-time
        mean_latency, p95_latency, answers = measure_queries(args.api)
        if args.mode == "local":
            toks, cost_per_1k = measure_ollama_toks(args.ollama_url, args.chat_model)
        else:
            toks, cost_per_1k = measure_azure_toks()
    finally:
        cleanup(args.api, doc_ids)

    results = json.loads(RESULTS_PATH.read_text()) if RESULTS_PATH.exists() else {}
    results[args.label] = {
        "inference_tok_s": round(toks, 1),
        "embedding_docs_per_min": round(docs_per_min, 1),
        "query_latency_mean_s": round(mean_latency, 2),
        "query_latency_p95_s": round(p95_latency, 2),
        "cost_per_1k_queries_usd": None if cost_per_1k is None else round(cost_per_1k, 2),
    }
    RESULTS_PATH.write_text(json.dumps(results, indent=2) + "\n")

    slug = args.label.lower().replace(" ", "-").replace("(", "").replace(")", "")
    answers_path = REPO / "benchmarks" / f"answers-{slug}.md"
    lines = [f"# Answers — {args.label}\n"]
    for entry in answers:
        lines.append(f"**Q: {entry['question']}** ({entry['sources']} sources)\n\n{entry['answer']}\n")
    answers_path.write_text("\n".join(lines))

    print(f"\nResults merged into {RESULTS_PATH.name}; answers in {answers_path.name}")
    for label, row in results.items():
        cost = "$0" if row["cost_per_1k_queries_usd"] is None else f"~${row['cost_per_1k_queries_usd']}"
        print(f"  {label}: {row['inference_tok_s']} tok/s | "
              f"{row['embedding_docs_per_min']} docs/min | "
              f"{row['query_latency_mean_s']}s mean (p95 {row['query_latency_p95_s']}s) | {cost}/1K queries")


if __name__ == "__main__":
    main()
