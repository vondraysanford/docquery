using System.Net.Http.Json;
using System.Text.Json;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;
using Microsoft.Extensions.Options;

namespace DocQuery.Providers.Local;

/// <summary>
/// Vector store implementation using ChromaDB (local, Docker-based).
/// Reference implementation — use this pattern for AzureSearchVectorStore.
///
/// Targets the Chroma v2 API (v1 returns 410 Gone on current server builds):
/// https://docs.trychroma.com/reference
/// </summary>
public class ChromaVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private const string CollectionName = "docquery";
    private const string ApiBase = "/api/v2/tenants/default_tenant/databases/default_database";
    private string? _collectionId;

    public ChromaVectorStore(HttpClient httpClient, IOptions<ChromaDbOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionId != null) return;

        // Cosine space: nomic-embed-text vectors are compared by cosine similarity,
        // and it keeps the distance→score conversion in SearchAsync meaningful.
        var request = new
        {
            name = CollectionName,
            get_or_create = true,
            configuration = new { hnsw = new { space = "cosine" } }
        };
        var response = await _httpClient.PostAsJsonAsync($"{ApiBase}/collections", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _collectionId = result.GetProperty("id").GetString();
    }

    public async Task StoreChunksAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            ids = chunks.Select(c => c.Id).ToList(),
            embeddings = chunks.Select(c => c.Embedding).ToList(),
            documents = chunks.Select(c => c.Content).ToList(),
            metadatas = chunks.Select(c => new Dictionary<string, string>(c.Metadata)
            {
                ["documentId"] = c.DocumentId,
                ["chunkIndex"] = c.ChunkIndex.ToString()
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{ApiBase}/collections/{_collectionId}/add", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            query_embeddings = new[] { queryEmbedding },
            n_results = topK,
            include = new[] { "documents", "metadatas", "distances" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{ApiBase}/collections/{_collectionId}/query", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        var chunks = new List<RetrievedChunk>();
        var documents = result.GetProperty("documents")[0];
        var metadatas = result.GetProperty("metadatas")[0];
        var distances = result.GetProperty("distances")[0];
        var ids = result.GetProperty("ids")[0];

        for (int i = 0; i < documents.GetArrayLength(); i++)
        {
            var metadata = metadatas[i];

            var metadataDict = new Dictionary<string, string>();
            foreach (var property in metadata.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    metadataDict[property.Name] = property.Value.GetString() ?? "";
            }

            chunks.Add(new RetrievedChunk
            {
                Score = 1.0 - distances[i].GetDouble(), // cosine distance → similarity
                Chunk = new DocumentChunk
                {
                    Id = ids[i].GetString() ?? "",
                    Content = documents[i].GetString() ?? "",
                    DocumentId = metadataDict.GetValueOrDefault("documentId", ""),
                    ChunkIndex = int.TryParse(metadataDict.GetValueOrDefault("chunkIndex"), out var idx) ? idx : 0,
                    Metadata = metadataDict
                }
            });
        }

        return chunks.OrderByDescending(c => c.Score).ToList();
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            where = new Dictionary<string, string> { ["documentId"] = documentId }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{ApiBase}/collections/{_collectionId}/delete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
