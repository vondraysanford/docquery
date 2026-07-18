namespace DocQuery.Core.Interfaces;

/// <summary>
/// Generates vector embeddings from text.
/// 
/// Implement this interface for each provider:
///   - Local: OllamaEmbeddingProvider (done — see DocQuery.Providers.Local)
///   - Azure: AzureOpenAIEmbeddingProvider (YOUR TURN — AI-102 practice!)
///
/// AI-102 study notes:
///   - Azure OpenAI embeddings use the text-embedding-ada-002 or text-embedding-3-small models
///   - You'll need: endpoint, API key, and deployment name
///   - SDK: Azure.AI.OpenAI NuGet package
///   - Docs: https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/embeddings
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generate an embedding vector for a single piece of text.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in a batch.
    /// More efficient than calling GenerateEmbeddingAsync in a loop.
    /// </summary>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
}
