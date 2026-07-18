using DocQuery.Core.Interfaces;

namespace DocQuery.Providers.Azure;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║  YOUR TURN — AI-102 PRACTICE                                    ║
/// ║                                                                  ║
/// ║  Implement this class using the Azure.AI.OpenAI SDK.             ║
/// ║  Use OllamaEmbeddingProvider as your reference pattern.          ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// 
/// STEP-BY-STEP GUIDE:
/// 
/// 1. Uncomment the NuGet packages in DocQuery.Providers.Azure.csproj
///    and run: dotnet restore
/// 
/// 2. Create an AzureOpenAIOptions class (like OllamaOptions) with:
///    - Endpoint (string)
///    - ApiKey (string)  
///    - EmbeddingDeployment (string) — e.g., "text-embedding-ada-002"
///    - ChatDeployment (string) — e.g., "gpt-4o"
/// 
/// 3. Implement GenerateEmbeddingAsync:
///    - Create an AzureOpenAIClient with your endpoint + key
///    - Call client.GetEmbeddingClient(deploymentName)
///    - Call embeddingClient.GenerateEmbeddingAsync(text)
///    - Return the embedding vector as float[]
/// 
/// 4. Implement GenerateEmbeddingsAsync:
///    - Azure OpenAI supports batch embeddings natively!
///    - Call embeddingClient.GenerateEmbeddingsAsync(texts) 
///    - Much faster than the Ollama sequential approach
/// 
/// KEY DIFFERENCES FROM OLLAMA (great interview talking point):
///   - Azure supports batch embedding (Ollama doesn't)
///   - Azure has built-in content filtering
///   - Azure requires explicit deployment names (not just model names)
///   - Azure uses API key or Azure AD auth (Ollama has no auth)
/// 
/// DOCS:
///   https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/embeddings
///   https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai
/// </summary>
public class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // TODO: Implement using Azure.AI.OpenAI SDK
        throw new NotImplementedException(
            "Implement this! See the XML docs above for step-by-step guidance.");
    }

    public Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        // TODO: Implement using Azure.AI.OpenAI SDK (batch endpoint)
        throw new NotImplementedException(
            "Implement this! Azure supports native batch embeddings — faster than Ollama.");
    }
}
