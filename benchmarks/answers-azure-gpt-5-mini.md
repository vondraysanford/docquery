# Answers — Azure (gpt-5-mini)

**Q: What exam did Vondray pass in June 2026?** (5 sources)

He passed the Microsoft Azure AI Engineer Associate exam (AI-102) in June 2026 — see "How Vondray Prepared for AI-102" in certifications.md and the certifications list (Microsoft Certified: Azure AI Engineer Associate (AI-102)).

**Q: Which embedding model does DocQuery use for local inference?** (5 sources)

DocQuery uses the nomic-embed-text embedding model for local inference — the project notes: "Each chunk is embedded using the `nomic-embed-text` model served by Ollama" (vectors stored in ChromaDB).

**Q: What graduate program is Vondray starting, and with which specialization?** (5 sources)

Vondray is starting Georgia Tech’s Online Master of Science in Computer Science (OMSCS), specializing in Machine Learning. (See "What's Next" in certifications.md.)

**Q: What are the three domain interfaces in DocQuery.Core?** (5 sources)

The three interfaces are IEmbeddingProvider, ILlmProvider, and IVectorStore — as stated in DocQuery.Core: "IEmbeddingProvider, ILlmProvider, and IVectorStore" (project-docquery.md). 

- IEmbeddingProvider — handles creating embeddings (e.g., Ollama embeddings).  
- ILlmProvider — handles the chat/LLM calls (e.g., Llama 3 via Ollama).  
- IVectorStore — handles storing/retrieving vectors (e.g., ChromaDB).  

These names and roles are described in the backend overview and provider-abstraction sections of project-docquery.md.

**Q: Where are DocQuery's vectors stored when running in local mode?** (5 sources)

In ChromaDB (running in Docker). The docs state that embeddings are produced via Ollama and "the vectors are stored in ChromaDB running in Docker" and the local provider implements the IVectorStore against ChromaDB.

**Q: What did Vondray build that reduced monthly support tickets by 90 percent?** (5 sources)

He built a C#/.NET bulk data administration tool for the NavCare Connect web portal. As described in the NavCare section, that tool “reduced the development team's monthly support ticket volume by 90% through streamlined data workflows.”

**Q: Which company does Vondray currently work for, and what is his title?** (5 sources)

According to the resume, Vondray currently works at CCCIS as a Software Engineer II (listed as "Software Engineer II — CCCIS (Remote), April 2022 – Present").

**Q: Name two courses or certifications Vondray has completed from Anthropic.** (5 sources)

According to certifications.md (June 2026), two Anthropic courses Vondray completed are "Claude Code in Action" and "Introduction to Subagents."

**Q: What kind of open source contributions has Vondray made?** (5 sources)

Two concrete contributions are listed in the provided material:

- MonoGame — XML API documentation
  - Wrote XML API docs for the GraphicsAdapter and Album classes (19 public members total).
  - Work involved adapting archived XNA reference material and adding platform‑specific remarks for DirectX, DesktopGL, iOS, and Android.
  - Pull requests: github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford (see open-source-contributions.md).

- KodeKloud AI-102 course repository — security fix
  - Discovered a hardcoded Azure Cognitive Services endpoint and live API key in a public course sample.
  - Submitted a fix replacing the live credentials with placeholder values to remove exposed credentials.
  - Pull request: github.com/kodekloudhub/AI-102/pull/1 (work came out of AI‑102 exam study).

Key terms: XML API documentation, MonoGame, GraphicsAdapter, Album, XNA reference, platform‑specific remarks, hardcoded credentials, Azure Cognitive Services, security fix, placeholder values.

**Q: What hardware does Vondray use for local AI inference at home?** (5 sources)

The notes say he runs smaller Ollama models directly on his MacBook for development at home. (He uses an NVIDIA DGX Spark with 128 GB unified memory as the heavier inference target, but the MacBook is used for local/home development.)
