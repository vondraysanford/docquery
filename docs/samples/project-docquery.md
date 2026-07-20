# DocQuery — A Local-First RAG Application in C#/.NET

## What DocQuery Is

DocQuery is a retrieval-augmented generation (RAG) application built by Vondray Sanford. Users upload technical documentation or study materials (PDF, Markdown, or plain text) and ask questions about them in plain English; every answer is grounded in cited source chunks retrieved from the uploaded documents. DocQuery has a C#/.NET 10 backend, a React frontend, and is designed around a swappable provider architecture that supports fully local inference today, with Azure AI services planned behind the same interfaces.

## Why Vondray Built DocQuery

Vondray conceived DocQuery while studying for the Azure AI-102 exam, which he passed in June 2026. He kept running into the same gap: most RAG tutorials assume Python and a single hosted provider. As a .NET engineer moving deeper into AI, Vondray wanted proof — for himself first — that a production-quality RAG pipeline can be built in the Microsoft ecosystem, architected so that local inference and Azure AI services are interchangeable behind clean interfaces.

DocQuery also has a personal use case: Vondray is starting Georgia Tech's Online Master of Science in Computer Science (OMSCS) with a Machine Learning specialization, and DocQuery will be loaded with course materials so he can quiz himself using his own pipeline. He is building the tool, then studying with it.

## How DocQuery Works

The DocQuery pipeline runs entirely on local hardware in Phase 1. Documents are uploaded through the React UI, parsed, and split into fixed-size chunks with overlap. Each chunk is embedded using the `nomic-embed-text` model served by Ollama, and the vectors are stored in ChromaDB running in Docker. When a user asks a question, DocQuery embeds the question, retrieves the top-k most similar chunks from ChromaDB, assembles them into a context window, and sends the context plus the question to a Llama 3 chat model via Ollama. The response is returned with the source chunks that grounded it, displayed in a dedicated sources pane in the UI.

The backend is organized as an ASP.NET Core Web API (`DocQuery.Api`) with domain interfaces in `DocQuery.Core` (`IEmbeddingProvider`, `ILlmProvider`, `IVectorStore`) and local implementations in `DocQuery.Providers.Local` (Ollama embedding and chat providers, ChromaDB vector store). Nothing network-related is hardcoded: Ollama and ChromaDB endpoints all come from configuration, so the same build runs against a laptop or a more powerful inference server.

## Build Status

As of July 2026, DocQuery's Phase 1 (local RAG MVP) is functionally complete: ingestion for PDF, Markdown, and plain text; embeddings via Ollama; vector storage in ChromaDB; the full query pipeline with cited answers; the React UI with upload, chat, and sources panes; and smoke tests for the ingestion and query paths. The remaining Phase 1 item is recording the demo.

Phase 2 (planned) implements the Azure providers — Azure OpenAI for embeddings and chat, Azure AI Search as the vector store — switchable via a single configuration value, followed by benchmarks comparing local and Azure stacks on speed, latency, cost, and answer quality. Phase 3 (planned) adds study mode (flashcard and quiz generation), streaming responses, hybrid search, and multi-document collections.

## Local Hardware: NVIDIA DGX Spark

Vondray's inference target for heavier models is an NVIDIA DGX Spark with 128 GB of unified memory; during development, smaller Ollama models run directly on his MacBook. Vondray has worked through most of NVIDIA's DGX Spark hands-on tutorials.

Beyond DocQuery, Vondray runs a personal agent stack on the DGX Spark named Hermes: a Qwen 3.6 model acting as the orchestrator, with a Qwen3 Coder 30B subagent, connected to an Obsidian vault that serves as its knowledge base. The Spark's local model library runs from 30B-class coder models up to NVIDIA's Nemotron 3 Super 120B, all served through Ollama on the machine's 128 GB of unified memory.

## Design Choices

The provider abstraction is in place from Phase 1: `DocQuery.Core` defines the `IEmbeddingProvider`, `ILlmProvider`, and `IVectorStore` interfaces, and `DocQuery.Providers.Local` implements them against Ollama and ChromaDB. What Vondray deliberately deferred is the Azure implementation — it waits until Phase 2, when there is a working local baseline worth benchmarking against, rather than building two providers speculatively before either is proven end-to-end.

DocQuery is a public portfolio project: the repository's README doubles as the build plan, and no feature is claimed as done unless it works end-to-end. The source is available at [github.com/vondraysanford/docquery](https://github.com/vondraysanford/docquery) under the MIT license.