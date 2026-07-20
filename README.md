# 📄 DocQuery

**Ask natural language questions over your documents — powered by local LLMs or Azure OpenAI.**

DocQuery is a retrieval-augmented generation (RAG) application: upload technical documentation or study materials and query them in plain English, with every answer grounded in cited source chunks. Built with a C#/.NET 10 backend and React frontend, designed around a swappable provider architecture that will support both fully local inference (Ollama on an NVIDIA DGX Spark) and Azure AI services.

> **✅ Status: Phase 1 (local RAG MVP) complete — Phase 2 (provider pattern + Azure mode) up next.**
> This README is the build plan as much as the documentation. Nothing is claimed as done unless its box is checked. Follow along: I'm building this in public.

---

## Why I'm Building This

I conceived DocQuery while studying for the Azure AI-102 exam (passed, June 2026) and kept running into the same gap: most RAG tutorials assume Python and a single hosted provider. As a .NET engineer moving deeper into AI, I wanted proof — for myself first — that you can build a production-quality RAG pipeline in the Microsoft ecosystem, and architect it so local inference and Azure AI services are interchangeable behind clean interfaces.

Now the study-partner use case has a new target: I'm starting Georgia Tech's OMSCS (Machine Learning specialization), and DocQuery will be loaded with course materials so I can quiz myself using my own pipeline. Building the tool, then studying with it.

---

## The Plan: Three Phases

| Phase | Theme | Outcome | Estimate |
|-------|-------|---------|----------|
| **1** | Make it work | A demoable local RAG app: upload → ask → cited answer | 2–3 weekends |
| **2** | Make it swappable | Provider pattern + Azure mode, benchmarked against local | 2 weekends |
| **3** | Make it mine | Study mode, streaming, hybrid search, polish | ongoing |

Each phase ends with something real: a demo, a benchmark table, a feature I use daily. No phase begins until the previous phase's "done when" is true.

---

## Phase 1 — Local RAG MVP

**Goal:** the smallest complete RAG loop, running entirely on local hardware, free, demoable offline. No provider abstraction yet — concrete classes, straight-line code.

- [x] .NET 10 Web API skeleton with health check endpoint
- [x] Document ingestion: PDF, Markdown, and plain-text upload → parse → chunk (fixed-size with overlap)
- [x] Embeddings via Ollama (`nomic-embed-text`)
- [x] Vector storage in ChromaDB (Docker container)
- [x] Query pipeline: embed question → top-k retrieval → context assembly → answer via Ollama (Llama 3) → response with source citations
- [x] React UI: upload panel, chat, and a sources pane showing exactly which chunks grounded each answer
- [x] Smoke tests for the ingestion and query paths
- [x] Demo GIF recorded and embedded below

**Done when:** a stranger can clone the repo, follow the Getting Started steps, upload a document, ask a question, and get a cited answer — and there's a GIF at the top of this README proving it.

![DocQuery demo: uploading OMSCS course documents and getting a cited answer](docs/demo.gif)

---

## Phase 2 — Provider Pattern + Azure Mode

**Goal:** extract the abstraction Phase 1 deliberately skipped, then implement it twice. One config flag switches the entire stack between local and Azure.

- [ ] Extract `IEmbeddingProvider`, `ILlmProvider`, and `IVectorStore` interfaces into `DocQuery.Core`; move Ollama/ChromaDB implementations into `DocQuery.Providers.Local`
- [ ] `DocQuery.Providers.Azure`: Azure OpenAI (embeddings + chat) and Azure AI Search (vector store)
- [ ] Provider switching via `appsettings.json` — no code changes to flip modes
- [ ] Session-scoped conversation memory (follow-up questions keep context)
- [ ] `docker-compose.yml` for one-command local stack
- [ ] **Benchmarks:** fill the table below with real measurements

| Metric | Local (Llama 3 8B) | Local (Llama 3 70B) | Azure (GPT-4o) |
|--------|--------------------|---------------------|----------------|
| Inference speed (tok/s) | — | — | — |
| Embedding throughput (docs/min) | — | — | — |
| Average query latency | — | — | — |
| Cost per 1K queries | $0 | $0 | — |
| Answer quality (subjective notes) | — | — | — |

**Done when:** the same UI runs against both stacks by changing one config value, and every cell in that table holds a measured number — the local-vs-Azure comparison is the most interesting output of this whole project.

---

## Phase 3 — Study Mode & Polish

**Goal:** turn a working pipeline into a tool I reach for daily, starting with OMSCS coursework.

- [ ] **Study mode:** generate flashcards and quiz questions from ingested documents
- [ ] Streaming responses
- [ ] Hybrid search (keyword + semantic)
- [ ] Multi-document collections (per-course, per-topic)
- [ ] DOCX and HTML ingestion
- [ ] Side-by-side provider comparison UI (same question, both stacks, answers side by side)

**Stretch ideas (beyond Phase 3):**
- Fine-tuned embedding model for domain-specific content
- An "Ask my portfolio" variant embedded at [vondraysanford.com](https://vondraysanford.com) — RAG over my resume and projects, running on the DGX Spark

**Done when:** I've used study mode for a real OMSCS assignment, and the "What I'm Learning" section below has an honest entry for every phase.

---

## Architecture

Target architecture (Phase 2+). Phase 1 implements the **Local** path only, without the provider layer — the abstraction is extracted in Phase 2 once there's working code to abstract.

```
┌─────────────────────────────────────────────────────────┐
│                    React Frontend                       │
│               (Upload, Chat, Sources)                   │
└───────────────────────┬─────────────────────────────────┘
                        │ REST API
┌───────────────────────▼─────────────────────────────────┐
│                 .NET 10 Web API                         │
│                                                         │
│   Ingestion            Retrieval          Generation    │
│   • parsing            • embedding        • context     │
│   • chunking           • similarity       • LLM query   │
│   • storage            • ranking          • citations   │
│                            │                            │
│                  ┌─────────▼──────────┐                 │
│                  │   Provider Layer   │   (Phase 2)     │
│                  │   IEmbedding /     │                 │
│                  │   ILlm / IVector   │                 │
│                  └────┬──────────┬────┘                 │
└───────────────────────┼──────────┼──────────────────────┘
                        │          │
              ┌─────────▼───┐  ┌───▼─────────────┐
              │    LOCAL    │  │      AZURE      │
              │ Ollama      │  │ Azure OpenAI    │
              │ Llama 3     │  │ Azure AI Search │
              │ ChromaDB    │  │                 │
              └─────┬───────┘  └─────────────────┘
                    │
        ┌───────────▼──────────┐
        │  NVIDIA DGX Spark    │       ☁️ Azure Cloud
        │  128 GB Unified Mem  │       (Phase 2)
        └──────────────────────┘
```

---

## Tech Stack

| Layer | Phase 1 (Local) | Phase 2 adds (Azure mode) |
|-------|-----------------|---------------------------|
| Frontend | React, JavaScript, CSS | — |
| Backend API | C# / .NET 10, ASP.NET Core | provider interfaces in `DocQuery.Core` |
| Vector store | ChromaDB (Docker) | Azure AI Search |
| Embeddings | Ollama (`nomic-embed-text`) | Azure OpenAI (`text-embedding-ada-002`) |
| LLM inference | Ollama (Llama 3 — 8B for dev, 70B fits on the Spark) | Azure OpenAI (GPT-4o) |
| Hardware | NVIDIA DGX Spark (128 GB unified memory) | Azure (free tier / pay-as-you-go) |

---

## Getting Started (Phase 1 — Local Mode)

Azure setup instructions will be added when Phase 2 lands. Everything below runs free on your own hardware.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22 LTS or newer](https://nodejs.org/) (the UI's build tool, Vite 8, requires Node ≥ 20.19)
- [Docker](https://www.docker.com/) (for ChromaDB)
- [Ollama](https://ollama.ai/) installed and running (any Ollama-capable machine works; a GPU helps)

### 1. Clone and configure

```bash
git clone https://github.com/vondraysanford/docquery.git
cd docquery
cp src/DocQuery.Api/appsettings.example.json src/DocQuery.Api/appsettings.json
```

```json
{
  "DocQuery": {
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "EmbeddingModel": "nomic-embed-text",
      "ChatModel": "llama3"
    },
    "ChromaDb": {
      "BaseUrl": "http://localhost:8000"
    }
  }
}
```

### 2. Start dependencies

```bash
docker run -d -p 8000:8000 chromadb/chroma
ollama pull llama3
ollama pull nomic-embed-text
```

### 3. Run the backend

```bash
cd src/DocQuery.Api
dotnet restore
dotnet run
```

The API listens on `http://localhost:5000` — verify with `curl http://localhost:5000/health`, which should return `{"status":"healthy"}`.

### 4. Run the frontend

```bash
cd src/docquery-ui
npm install
npm start
```

Open `http://localhost:3000`, upload a PDF or Markdown file, and ask it something. (The UI dev server proxies `/api` requests to the backend on port 5000 — no extra configuration needed.)

Need a document to try? The repo ships a small sample corpus in [`docs/samples/`](docs/samples/) — my resume, project notes, and certification history. Upload a file or two and ask things like *"What Azure experience does Vondray have?"* or *"Why was DocQuery built in C# instead of Python?"* — the sources pane will show exactly which chunks grounded the answer.

**Shortcut:** once dependencies are installed, `./start.sh` from the repo root runs steps 3 and 4 together in one terminal — Ctrl+C stops both.

### 5. (Optional) Run the smoke tests

```bash
dotnet test tests/DocQuery.Api.Tests
```

The tests use fake providers, so they pass without Ollama, ChromaDB, or Docker running.

---

## Project Structure

```
docquery/
├── src/
│   ├── DocQuery.Api/               # ASP.NET Core Web API — controllers, file parsing, composition root
│   │   ├── Controllers/            #   DocumentsController (ingestion), QueryController (RAG loop)
│   │   ├── Services/               #   DocumentTextExtractor (PDF via PdfPig, Markdown/text)
│   │   └── Program.cs
│   ├── DocQuery.Core/              # Interfaces (IEmbeddingProvider, ILlmProvider, IVectorStore),
│   │                               #   domain models, ChunkingService
│   ├── DocQuery.Providers.Local/   # Ollama (embeddings + chat) and ChromaDB implementations
│   ├── DocQuery.Providers.Azure/   # Phase 2: Azure OpenAI + AI Search (stubs, not yet referenced)
│   └── docquery-ui/                # React frontend (Vite) — upload, chat, sources pane
├── tests/
│   └── DocQuery.Api.Tests/         # Smoke tests — fake providers, no services required
├── docs/                           # Demo GIF, architecture notes
├── start.sh                        # Runs API + UI together for local dev
└── README.md
```

The interface/provider split emerged during Phase 1 rather than waiting for Phase 2: the contracts live in `DocQuery.Core`, the Ollama/ChromaDB implementations in `DocQuery.Providers.Local`. What remains for Phase 2 is implementing the same interfaces against Azure (`DocQuery.Providers.Azure` is stubbed but deliberately unreferenced) and the config flag that swaps the stacks.

---

## What I'm Learning

Updated at the end of each phase — honest notes on what was harder than expected, what I'd do differently, and what the tutorials don't tell you.

- **Phase 1:** _pending_
- **Phase 2:** _pending — including the local-vs-Azure quality/cost/latency verdict_
- **Phase 3:** _pending_

---

## Related

- Blog: posts on this build will appear at [vondraysanford.com](https://vondraysanford.com)
- Portfolio: [vondraysanford.com](https://vondraysanford.com)

---

## License

MIT

---

**Built by [Vondray Sanford](https://www.linkedin.com/in/vondray-sanford/)** — .NET engineer building at the intersection of enterprise systems and modern AI.
