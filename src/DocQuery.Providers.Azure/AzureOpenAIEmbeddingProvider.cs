using Azure;
using Azure.AI.OpenAI;
using DocQuery.Core.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace DocQuery.Providers.Azure;

/// <summary>
/// Generates embeddings using an Azure OpenAI deployment (provisioned via
/// Azure AI Foundry), behind the same IEmbeddingProvider contract as the
/// Ollama implementation.
///
/// Key difference from Ollama: Azure embeds a whole batch in one call,
/// where Ollama requires one request per text.
/// </summary>
public class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingClient _client;

    public AzureOpenAIEmbeddingProvider(IOptions<AzureOpenAIOptions> options)
    {
        var config = options.Value;
        var azureClient = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new AzureKeyCredential(config.ApiKey));
        _client = azureClient.GetEmbeddingClient(config.EmbeddingDeployment);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var response = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var response = await _client.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);
        return response.Value
            .OrderBy(embedding => embedding.Index)
            .Select(embedding => embedding.ToFloats().ToArray())
            .ToList();
    }
}
