using System.Collections.Concurrent;
using DocQuery.Api.Services;
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
    private readonly string _storeKey;

    // In-memory document tracking, scoped per vector store so the list stays
    // truthful when switching providers. Profiles that share a store (Local
    // and Spark on the same ChromaDB) share a registry; Azure gets its own.
    // Still forgotten on restart — a known limitation for the demo-mode work.
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Document>> _documentsByStore = new();

    private ConcurrentDictionary<string, Document> Documents
        => _documentsByStore.GetOrAdd(_storeKey, _ => new ConcurrentDictionary<string, Document>());

    public DocumentsController(IProviderContext providers, ChunkingService chunkingService)
    {
        _embeddingProvider = providers.Embeddings;
        _vectorStore = providers.VectorStore;
        _storeKey = providers.StoreKey;
        _chunkingService = chunkingService;
    }

    /// <summary>
    /// Upload a document for ingestion into the RAG pipeline.
    /// Accepts a PDF, Markdown, or plain-text file as multipart/form-data.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        if (!DocumentTextExtractor.IsSupported(file.FileName))
            return BadRequest("Unsupported file type. Upload a .pdf, .md, or .txt file.");

        // 1. Parse the file into plain text
        string content;
        try
        {
            content = await DocumentTextExtractor.ExtractAsync(file, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BadRequest($"Could not parse '{file.FileName}': {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest($"No text could be extracted from '{file.FileName}'.");

        var document = new Document
        {
            FileName = file.FileName,
            Content = content
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
        Documents[document.Id] = document;

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
        var docs = Documents.Values
            .OrderBy(d => d.UploadedAt)
            .Select(d => new
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
        if (!Documents.ContainsKey(documentId))
            return NotFound();

        await _vectorStore.DeleteDocumentAsync(documentId, cancellationToken);
        Documents.TryRemove(documentId, out _);

        return NoContent();
    }
}
