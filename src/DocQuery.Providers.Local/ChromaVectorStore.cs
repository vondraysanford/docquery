using System.Net.Http.Json;
using System.Text.Json;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;

namespace DocQuery.Providers.Local;

/// <summary>
/// Vector store implementation using ChromaDB (local, Docker-based).
/// Reference implementation — use this pattern for AzureSearchVectorStore.
/// 
/// ChromaDB API: https://docs.trychroma.com/reference
/// Assumes ChromaDB is running on http://localhost:8000
/// </summary>
public class ChromaVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private const string CollectionName = "docquery";
    private string? _collectionId;

    public ChromaVectorStore(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:8000");
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionId != null) return;

        var request = new { name = CollectionName, get_or_create = true };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/collections", request, cancellationToken);
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
            $"/api/v1/collections/{_collectionId}/add", request, cancellationToken);
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
            $"/api/v1/collections/{_collectionId}/query", request, cancellationToken);
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
            chunks.Add(new RetrievedChunk
            {
                Score = 1.0 - distances[i].GetDouble(), // ChromaDB returns distance; convert to similarity
                Chunk = new DocumentChunk
                {
                    Id = ids[i].GetString() ?? "",
                    Content = documents[i].GetString() ?? "",
                    DocumentId = metadata.TryGetProperty("documentId", out var docId) ? docId.GetString() ?? "" : "",
                    ChunkIndex = metadata.TryGetProperty("chunkIndex", out var idx) ? int.Parse(idx.GetString() ?? "0") : 0
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
            $"/api/v1/collections/{_collectionId}/delete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
