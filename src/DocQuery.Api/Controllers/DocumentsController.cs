using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;
using DocQuery.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly ChunkingService _chunkingService;

    // In-memory document tracking (swap for a database in production)
    private static readonly Dictionary<string, Document> _documents = new();

    public DocumentsController(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        ChunkingService chunkingService)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _chunkingService = chunkingService;
    }

    /// <summary>
    /// Upload a document for ingestion into the RAG pipeline.
    /// Accepts plain text content in the request body.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] UploadRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content is required.");

        // 1. Create document record
        var document = new Document
        {
            FileName = request.FileName ?? "untitled.txt",
            Content = request.Content
        };

        // 2. Chunk the document
        var chunkTexts = _chunkingService.ChunkText(document.Content);

        // 3. Generate embeddings for each chunk
        var embeddings = await _embeddingProvider.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

        // 4. Create DocumentChunk objects
        var chunks = chunkTexts.Select((text, i) => new DocumentChunk
        {
            DocumentId = document.Id,
            Content = text,
            ChunkIndex = i,
            Embedding = embeddings[i],
            Metadata = new Dictionary<string, string>
            {
                ["fileName"] = document.FileName
            }
        }).ToList();

        // 5. Store in vector database
        await _vectorStore.StoreChunksAsync(chunks, cancellationToken);

        // 6. Track the document
        _documents[document.Id] = document;

        return Ok(new
        {
            documentId = document.Id,
            fileName = document.FileName,
            chunksCreated = chunks.Count
        });
    }

    /// <summary>
    /// List all uploaded documents.
    /// </summary>
    [HttpGet]
    public IActionResult List()
    {
        var docs = _documents.Values.Select(d => new
        {
            d.Id,
            d.FileName,
            d.UploadedAt
        });

        return Ok(docs);
    }

    /// <summary>
    /// Delete a document and its chunks from the vector store.
    /// </summary>
    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(string documentId, CancellationToken cancellationToken)
    {
        if (!_documents.ContainsKey(documentId))
            return NotFound();

        await _vectorStore.DeleteDocumentAsync(documentId, cancellationToken);
        _documents.Remove(documentId);

        return NoContent();
    }
}

public class UploadRequest
{
    public string? FileName { get; set; }
    public string Content { get; set; } = string.Empty;
}
