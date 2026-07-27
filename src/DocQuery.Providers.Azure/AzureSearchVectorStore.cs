using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;
using Microsoft.Extensions.Options;

namespace DocQuery.Providers.Azure;

/// <summary>
/// Vector store implementation using Azure AI Search, behind the same
/// IVectorStore contract as the ChromaDB implementation.
///
/// Key differences from ChromaDB: the index schema is explicit (defined below
/// and created on first use), filtering uses OData expressions, and the same
/// index could serve hybrid vector + keyword search later.
/// </summary>
public class AzureSearchVectorStore : IVectorStore
{
    private const string VectorProfile = "docquery-vector-profile";
    private const string HnswAlgorithm = "docquery-hnsw";

    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;
    private bool _indexEnsured;

    public AzureSearchVectorStore(IOptions<AzureSearchOptions> options)
    {
        _options = options.Value;
        var endpoint = new Uri(_options.Endpoint);
        var credential = new AzureKeyCredential(_options.ApiKey);
        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexEnsured) return;

        var index = new SearchIndex(_options.IndexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                new SearchableField("content"),
                new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("chunkIndex", SearchFieldDataType.Int32),
                new SimpleField("fileName", SearchFieldDataType.String),
                new SimpleField("contentHash", SearchFieldDataType.String),
                new SearchField("embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = _options.VectorDimensions,
                    VectorSearchProfileName = VectorProfile,
                },
            },
            VectorSearch = new VectorSearch
            {
                Profiles = { new VectorSearchProfile(VectorProfile, HnswAlgorithm) },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(HnswAlgorithm)
                    {
                        Parameters = new HnswParameters { Metric = VectorSearchAlgorithmMetric.Cosine },
                    },
                },
            },
        };

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
        _indexEnsured = true;
    }

    public async Task StoreChunksAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        var documents = chunks.Select(chunk => new SearchDocument
        {
            ["id"] = chunk.Id,
            ["content"] = chunk.Content,
            ["documentId"] = chunk.DocumentId,
            ["chunkIndex"] = chunk.ChunkIndex,
            ["fileName"] = chunk.Metadata.GetValueOrDefault("fileName", ""),
            ["contentHash"] = chunk.Metadata.GetValueOrDefault("contentHash", ""),
            ["embedding"] = chunk.Embedding,
        });

        var batch = IndexDocumentsBatch.MergeOrUpload(documents);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        var searchOptions = new SearchOptions
        {
            Size = topK,
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryEmbedding)
                    {
                        KNearestNeighborsCount = topK,
                        Fields = { "embedding" },
                    },
                },
            },
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(
            searchText: null, searchOptions, cancellationToken);

        var chunks = new List<RetrievedChunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;
            var metadata = new Dictionary<string, string>
            {
                ["fileName"] = document.GetString("fileName") ?? "",
                ["documentId"] = document.GetString("documentId") ?? "",
                ["chunkIndex"] = document.GetInt32("chunkIndex")?.ToString() ?? "0",
            };

            chunks.Add(new RetrievedChunk
            {
                // Azure returns a similarity score (higher = closer) for cosine.
                Score = result.Score ?? 0,
                Chunk = new DocumentChunk
                {
                    Id = document.GetString("id") ?? "",
                    Content = document.GetString("content") ?? "",
                    DocumentId = metadata["documentId"],
                    ChunkIndex = int.TryParse(metadata["chunkIndex"], out var index) ? index : 0,
                    Metadata = metadata,
                },
            });
        }

        return chunks.OrderByDescending(c => c.Score).ToList();
    }

    public async Task<string?> GetDocumentFingerprintAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        var filter = $"documentId eq '{documentId.Replace("'", "''")}'";
        var options = new SearchOptions { Filter = filter, Size = 1 };
        options.Select.Add("contentHash");

        var response = await _searchClient.SearchAsync<SearchDocument>(
            searchText: null, options, cancellationToken);

        await foreach (var result in response.Value.GetResultsAsync())
            return result.Document.GetString("contentHash") ?? "";

        return null;
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await EnsureIndexAsync(cancellationToken);

        // OData filter; single quotes in the id are escaped by doubling.
        var filter = $"documentId eq '{documentId.Replace("'", "''")}'";
        var findOptions = new SearchOptions { Filter = filter, Size = 1000 };
        findOptions.Select.Add("id");

        var response = await _searchClient.SearchAsync<SearchDocument>(
            searchText: null, findOptions, cancellationToken);

        var ids = new List<string>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var id = result.Document.GetString("id");
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }

        if (ids.Count == 0) return;

        var batch = IndexDocumentsBatch.Delete("id", ids);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }
}
