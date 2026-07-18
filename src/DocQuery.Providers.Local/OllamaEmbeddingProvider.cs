using System.Net.Http.Json;
using System.Text.Json;
using DocQuery.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DocQuery.Providers.Local;

/// <summary>
/// Generates embeddings using a locally-running Ollama instance.
/// This is the reference implementation — study this pattern, then implement
/// AzureOpenAIEmbeddingProvider using the same interface.
/// </summary>
public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingProvider(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new { model = _options.EmbeddingModel, prompt = text };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var embedding = result.GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();

        return embedding;
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        // Ollama doesn't have a native batch endpoint, so we process sequentially.
        // Azure OpenAI DOES support batching — that's one advantage you'll see
        // when you implement the Azure provider.
        var embeddings = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
            embeddings.Add(embedding);
        }
        return embeddings;
    }
}
