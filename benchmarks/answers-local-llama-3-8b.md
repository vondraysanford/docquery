# Answers — Local (Llama 3 8B)

**Q: What exam did Vondray pass in June 2026?** (5 sources)

According to the certifications.md file, Vondray passed the Microsoft Certified: Azure AI Engineer Associate (AI-102) exam in June 2026.

**Q: Which embedding model does DocQuery use for local inference?** (5 sources)

According to the provided context, DocQuery uses the `nomic-embed-text` model served by Ollama for local inference.

**Q: What graduate program is Vondray starting, and with which specialization?** (5 sources)

According to the context from `certifications.md`, Vondray Sanford is starting Georgia Tech's Online Master of Science in Computer Science (OMSCS), specializing in Machine Learning.

**Q: What are the three domain interfaces in DocQuery.Core?** (5 sources)

Based on the provided context, the three domain interfaces in `DocQuery.Core` are:

1. `IEmbeddingProvider`
2. `ILlmProvider`
3. `IVectorStore`

These interfaces define the provider abstraction for embeddings, LLaMA chat models, and vector storage, respectively.

**Q: Where are DocQuery's vectors stored when running in local mode?** (5 sources)

According to the provided context (project-docquery.md), when running in local mode, DocQuery stores its vectors in ChromaDB, which runs in Docker.

**Q: What did Vondray build that reduced monthly support tickets by 90 percent?** (5 sources)

According to the context from documents, Vondray built a C#/.NET bulk data administration tool at NavCare, which reduced the development team's monthly support ticket volume by 90%.

**Q: Which company does Vondray currently work for, and what is his title?** (5 sources)

According to the provided context from `resume.md`, Vondray currently works for CCCIS (Remote) as a Software Engineer II.

**Q: Name two courses or certifications Vondray has completed from Anthropic.** (5 sources)

According to the provided context, Vondray has completed the following two certifications from Anthropic:

1. **Introduction to Subagents**
2. **Introduction to Agent Skills**

**Q: What kind of open source contributions has Vondray made?** (5 sources)

According to the provided context, Vondray Sanford has made two open-source contributions:

1. **MonoGame: API Documentation for a 14K-Star C#/.NET Game Framework**: Vondray contributed XML API documentation to MonoGame across two pull requests, covering the `GraphicsAdapter` and `Album` classes - 19 public members in total.
2. **KodeKloud AI-102 Course Repository: Security Fix for Exposed Credentials**: While studying for the Azure AI Engineer (AI-102) certification using KodeKloud's official course repository, Vondray identified a hardcoded Azure Cognitive Services endpoint and live API key exposed in a public course code sample. He submitted a security fix replacing the live credentials with placeholder values.

These contributions were made to open-source projects on GitHub: MonoGame at [github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford](https://github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford) and KodeKloud's AI-102 course repository at [github.com/kodekloudhub/AI-102/pull/1](https://github.com/kodekloudhub/AI-102/pull/1).

**Q: What hardware does Vondray use for local AI inference at home?** (5 sources)

According to the context, Vondray uses an NVIDIA DGX Spark with 128 GB of unified memory for heavier models. He also runs a personal agent stack on this device named Hermes: a Qwen 3.6 model acting as the orchestrator, with a Qwen3 Coder 30B subagent, connected to an Obsidian vault that serves as its knowledge base.
