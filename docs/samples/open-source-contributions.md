# Vondray Sanford — Open Source Contributions

## MonoGame: API Documentation for a 14K-Star C#/.NET Game Framework

MonoGame is a cross-platform C#/.NET game framework with more than 14,000 stars on GitHub, used to build games that run on desktop, mobile, and console platforms. Vondray Sanford contributed XML API documentation to MonoGame across two pull requests, covering the `GraphicsAdapter` and `Album` classes — 19 public members in total.

The documentation work involved adapting archived XNA reference material (MonoGame is the community continuation of Microsoft's XNA framework) and adding platform-specific remarks describing runtime behavior differences across DirectX, DesktopGL, iOS, and Android. Vondray's MonoGame pull requests are listed at [github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford](https://github.com/MonoGame/MonoGame/pulls?q=author%3Avondraysanford).

## KodeKloud AI-102 Course Repository: Security Fix for Exposed Credentials

While studying for the Azure AI Engineer (AI-102) certification using KodeKloud's official course repository, Vondray identified a hardcoded Azure Cognitive Services endpoint and live API key exposed in a public course code sample. Anyone cloning the public repository could have used the live credentials.

Vondray submitted a security fix replacing the live credentials with placeholder values. The pull request is at [github.com/kodekloudhub/AI-102/pull/1](https://github.com/kodekloudhub/AI-102/pull/1). This contribution came directly out of Vondray's AI-102 exam preparation in 2026 — an example of treating study materials with the same scrutiny as production code.
