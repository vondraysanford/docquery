using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;

namespace DocQuery.Providers.Azure;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║  YOUR TURN — AI-102 PRACTICE                                    ║
/// ║                                                                  ║
/// ║  Implement this class using the Azure.Search.Documents SDK.      ║
/// ║  Use ChromaVectorStore as your reference pattern.                 ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// 
/// This is the most complex provider to implement and covers the most
/// AI-102 exam material. Take your time with this one.
///
/// STEP-BY-STEP GUIDE:
///
/// 1. Create an AzureSearchOptions class with:
///    - Endpoint (string)
///    - ApiKey (string)
///    - IndexName (string) — e.g., "docquery-index"
///
/// 2. Create a SearchIndexClient to manage the index:
///    - Define a SearchIndex with fields:
///      • id (string, key)
///      • content (string, searchable)
///      • documentId (string, filterable)
///      • chunkIndex (int)
///      • embedding (Collection(Edm.Single), vector searchable)
///    - Configure a vector search profile with HNSW algorithm
///    - Call indexClient.CreateOrUpdateIndexAsync()
///
/// 3. Implement StoreChunksAsync:
///    - Create a SearchClient for the index
///    - Map DocumentChunks to your index document model
///    - Call searchClient.IndexDocumentsAsync() with MergeOrUpload action
///
/// 4. Implement SearchAsync:
///    - Use SearchOptions with VectorSearch configured
///    - Create a VectorizedQuery with your query embedding
///    - Call searchClient.SearchAsync()
///    - Map results back to RetrievedChunk
///
/// 5. Implement DeleteDocumentAsync:
///    - Search for all chunks with matching documentId filter
///    - Call searchClient.IndexDocumentsAsync() with Delete action
///
/// AI-102 EXAM TOPICS THIS COVERS:
///   - Creating and configuring Azure AI Search indexes
///   - Vector search profiles and algorithms (HNSW vs exhaustive KNN)
///   - Index document schema design
///   - Hybrid search (vector + keyword) — BONUS if you implement this
///   - Filtering and faceting
///   - Indexers and skillsets (not needed here, but know the concepts)
///
/// KEY DIFFERENCES FROM CHROMADB (great interview talking point):
///   - Azure AI Search supports hybrid search (vector + BM25 keyword)
///   - Azure has built-in semantic ranking
///   - Azure supports complex filtering with OData expressions
///   - Azure scales horizontally with replicas and partitions
///   - ChromaDB is simpler but limited to vector-only search
///
/// DOCS:
///   https://learn.microsoft.com/en-us/azure/search/vector-search-overview
///   https://learn.microsoft.com/en-us/azure/search/search-get-started-vector
///   https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents
/// </summary>
public class AzureSearchVectorStore : IVectorStore
{
    public Task StoreChunksAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        // TODO: Implement using Azure.Search.Documents SDK
        throw new NotImplementedException(
            "Implement this! This is the meatiest provider — see the XML docs above.");
    }

    public Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement vector search
        // BONUS: Implement hybrid search (vector + keyword) for better results
        throw new NotImplementedException(
            "Implement this! Use VectorizedQuery with your embedding.");
    }

    public Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        // TODO: Filter by documentId, then delete matching documents
        throw new NotImplementedException(
            "Implement this! Use OData filter: documentId eq 'value'");
    }
}
