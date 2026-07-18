using DocQuery.Core.Models;

namespace DocQuery.Core.Interfaces;

/// <summary>
/// Stores and retrieves document chunks using vector similarity search.
/// 
/// Implement this interface for each provider:
///   - Local: ChromaVectorStore (done — see DocQuery.Providers.Local)
///   - Azure: AzureSearchVectorStore (YOUR TURN — AI-102 practice!)
///
/// AI-102 study notes:
///   - Azure AI Search supports vector search, keyword search, and hybrid
///   - You'll need to create an index with a vector field (Collection(Edm.Single))
///   - Key concepts: indexes, documents, indexers, skillsets, vector profiles
///   - SDK: Azure.Search.Documents NuGet package
///   - Docs: https://learn.microsoft.com/en-us/azure/search/vector-search-overview
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Store document chunks with their embeddings.
    /// </summary>
    Task StoreChunksAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for the most similar chunks to a query embedding.
    /// Returns chunks ranked by similarity score, highest first.
    /// </summary>
    Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all chunks belonging to a specific document.
    /// </summary>
    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}
