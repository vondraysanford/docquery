# DocQuery — A C#/.NET RAG Application with Swappable Local and Azure Providers

## What DocQuery Is

DocQuery is a retrieval-augmented generation (RAG) application built by Vondray Sanford. Users upload technical documentation or study materials (PDF, Markdown, or plain text) and ask questions about them in plain English; every answer is grounded in cited source chunks retrieved from the uploaded documents. DocQuery has a C#/.NET 10 backend, a React frontend, and a swappable provider architecture: the same code runs fully local inference (Ollama + ChromaDB) or Azure AI services (Azure OpenAI + Azure AI Search) behind the same interfaces, switchable at runtime. A live public demo runs at [docquery.vondraysanford.com](https://docquery.vondraysanford.com): "Ask my portfolio," a read-only deployment where visitors ask questions about Vondray's work and get cited answers drawn from his resume, certifications, and project documents (including this one).

## Why Vondray Built DocQuery

Vondray conceived DocQuery while studying for the Azure AI-102 exam, which he passed in June 2026. He kept running into the same gap: most RAG tutorials assume Python and a single hosted provider. As a .NET engineer moving deeper into AI, Vondray wanted proof (for himself first) that a production-quality RAG pipeline can be built in the Microsoft ecosystem, architected so that local inference and Azure AI services are interchangeable behind clean interfaces.

DocQuery also has a personal use case: Vondray is starting Georgia Tech's Online Master of Science in Computer Science (OMSCS) with a Machine Learning specialization in Spring 2027, and DocQuery will be loaded with course materials so he can quiz himself using his own pipeline. He is building the tool, then studying with it.

## How DocQuery Works

Documents are uploaded through the React UI, parsed, and split into fixed-size chunks with overlap. Each chunk is embedded and the vectors are stored; when a user asks a question, DocQuery embeds the question, retrieves the top-k most similar chunks, assembles them into a context window, and sends the context plus the question to a chat model. The answer streams back token by token alongside the source chunks that grounded it, displayed in a dedicated sources pane in the UI.

The backend is organized as an ASP.NET Core Web API (`DocQuery.Api`) with domain interfaces in `DocQuery.Core` (`IEmbeddingProvider`, `ILlmProvider`, `IVectorStore`) and two complete implementations: `DocQuery.Providers.Local` (`nomic-embed-text` embeddings and Llama 3 chat via Ollama, ChromaDB vector store running in Docker) and `DocQuery.Providers.Azure` (Azure OpenAI embeddings and chat, Azure AI Search vector store). Providers are registered as keyed services and selected per request, so the UI offers a runtime provider selector and a side-by-side comparison mode that streams the same question through two stacks at once. Nothing network-related is hardcoded: every endpoint comes from configuration, so the same build runs against a laptop, a more powerful inference server, or Azure.

## Build Status

As of July 2026, Phases 1 through 4 are complete and verified end-to-end. Phase 1 delivered the local RAG MVP: ingestion for PDF, Markdown, and plain text, embeddings via Ollama, vector storage in ChromaDB, the full query pipeline with cited answers, the React UI, and smoke tests. Phase 2 added the Azure providers (Azure OpenAI for embeddings and chat, Azure AI Search as the vector store), switchable via a single configuration value, plus measured benchmarks comparing the stacks. Phase 3 added streaming responses over server-sent events, a runtime provider selector, and side-by-side provider comparison. Phase 4 put the demo on the public internet: the UI on Cloudflare Pages at docquery.vondraysanford.com and the API in Azure Container Apps, running the Azure provider end-to-end in a hardened read-only demo mode.

The benchmarks produced the project's key findings: a local Llama 3 8B answers end-to-end in about 2.4 seconds (faster than Azure's gpt-5-mini at 4.3) because there is no network round trip, while Azure wins on throughput (85 tokens/sec) and graded answer quality (9.0 vs 8.4). Llama 3 70B on the DGX Spark showed that "fits in memory" is not "fast": it generates at only ~5.8 tokens/sec because memory bandwidth, not capacity, is the ceiling. Azure costs came out around $1.83 per thousand queries at list prices.

Phase 5 (planned) turns DocQuery into a daily study tool for OMSCS: hybrid search, multi-document collections, DOCX and HTML ingestion, and a study mode that generates flashcards and quiz questions from ingested documents.

## Local Hardware: NVIDIA DGX Spark

Vondray's inference target for heavier models is an NVIDIA DGX Spark with 128 GB of unified memory; during development, smaller Ollama models run directly on his MacBook. Vondray has worked through most of NVIDIA's DGX Spark hands-on tutorials.

Beyond DocQuery, Vondray runs a personal agent stack on the DGX Spark named Hermes: a Qwen 3.6 model acting as the orchestrator, with a Qwen3 Coder 30B subagent, connected to an Obsidian vault that serves as its knowledge base. The Spark's local model library runs from 30B-class coder models up to NVIDIA's Nemotron 3 Super 120B, all served through Ollama on the machine's 128 GB of unified memory.

## Design Choices

The provider abstraction was in place from Phase 1: `DocQuery.Core` defines the `IEmbeddingProvider`, `ILlmProvider`, and `IVectorStore` interfaces, and `DocQuery.Providers.Local` implements them against Ollama and ChromaDB. Vondray deliberately deferred the Azure implementation until Phase 2, when there was a working local baseline worth benchmarking against, rather than building two providers speculatively before either was proven end-to-end. That discipline paid off: the Azure providers slotted in behind the existing interfaces with no changes to the API or UI, and provider switching became a config value, later a per-request runtime choice via keyed services. One lesson the architecture surfaced: chat models are freely swappable, but an embedding model and its vector store are a committed pair: vectors from different embedding models cannot be mixed in one index.

DocQuery is a public portfolio project: the repository's README doubles as the build plan, and no feature is claimed as done unless it works end-to-end. The source is available at [github.com/vondraysanford/docquery](https://github.com/vondraysanford/docquery) under the MIT license.