namespace DocQuery.Core.Models;

/// <summary>
/// A document that has been uploaded for querying.
/// </summary>
public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// A chunk of a document after splitting. This is the unit that gets embedded and stored.
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// A retrieved chunk with its similarity score, returned from vector search.
/// </summary>
public class RetrievedChunk
{
    public DocumentChunk Chunk { get; set; } = new();
    public double Score { get; set; }
}

/// <summary>
/// A user's query and the RAG pipeline's response.
/// </summary>
public class QueryRequest
{
    public string Question { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
}

/// <summary>
/// The response from the RAG pipeline, including the answer and sources used.
/// </summary>
public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceReference> Sources { get; set; } = new();
    public string ConversationId { get; set; } = string.Empty;
}

/// <summary>
/// A reference to a source chunk that was used to generate the answer.
/// </summary>
public class SourceReference
{
    public string DocumentName { get; set; } = string.Empty;
    public string ChunkContent { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
