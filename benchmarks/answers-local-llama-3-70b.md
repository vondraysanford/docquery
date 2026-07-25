# Answers — Local (Llama 3 70B)

**Q: What exam did Vondray pass in June 2026?** (5 sources)

Vondray passed the Azure AI Engineer Associate (AI-102) exam in June 2026.

**Q: Which embedding model does DocQuery use for local inference?** (5 sources)

According to the provided context, specifically in the "How DocQuery Works" section of project-docquery.md, DocQuery uses the `nomic-embed-text` model served by Ollama for embeddings during local inference.

**Q: What graduate program is Vondray starting, and with which specialization?** (5 sources)

Vondray is starting Georgia Tech's Online Master of Science in Computer Science (OMSCS), specializing in Machine Learning.

**Q: What are the three domain interfaces in DocQuery.Core?** (5 sources)

According to the provided context, specifically in [Source: project-docquery.md], the three domain interfaces in `DocQuery.Core` are:

1. `IEmbeddingProvider`
2. `ILlmProvider`
3. `IVectorStore`

These interfaces define the provider abstraction in DocQuery, allowing for swappable implementations of embedding, language model (LLM), and vector store providers.

**Q: Where are DocQuery's vectors stored when running in local mode?** (5 sources)

According to the context from the documents, when running in local mode, DocQuery's vectors are stored in ChromaDB, which is running in a Docker container. (Source: project-docquery.md)

**Q: What did Vondray build that reduced monthly support tickets by 90 percent?** (5 sources)

Vondray built a C#/.NET bulk data administration tool that reduced the development team's monthly support ticket volume by 90% through streamlined data workflows.

**Q: Which company does Vondray currently work for, and what is his title?** (5 sources)

According to the provided context, Vondray Sanford currently works for CCCIS as a Software Engineer II (Remote), where he designs, develops, and maintains full-stack enterprise web applications.

**Q: Name two courses or certifications Vondray has completed from Anthropic.** (5 sources)

Two courses or certifications Vondray has completed from Anthropic are:

1. **Claude Code in Action**
2. **Introduction to Subagents**

**Q: What kind of open source contributions has Vondray made?** (5 sources)

Vondray Sanford has made two open-source contributions:

1. **MonoGame API Documentation**: Vondray contributed XML API documentation to MonoGame, a cross-platform C#/.NET game framework with over 14,000 stars on GitHub. His work covered the `GraphicsAdapter` and `Album` classes, involving 19 public members in total.

2. **KodeKloud AI-102 Course Repository Security Fix**: Vondray identified a hardcoded Azure Cognitive Services endpoint and live API key exposed in a public course code sample while studying for the Azure AI Engineer (AI-102) certification using KodeKloud's official course repository. He submitted a security fix replacing the live credentials with placeholder values.

Both contributions are listed on GitHub: [github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford](https://github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford) and [github.com/kodekloudhub/AI-102/pull/1](https://github.com/kodekloudhub/AI-102/pull/1).

**Q: What hardware does Vondray use for local AI inference at home?** (5 sources)

According to the provided context, Vondray uses an NVIDIA DGX Spark with 128 GB of unified memory as his inference target for heavier models at home. During development, smaller Ollama models run directly on his MacBook.
