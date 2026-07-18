using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILlmProvider _llmProvider;
    private readonly IVectorStore _vectorStore;

    // Simple in-memory conversation store
    private static readonly Dictionary<string, List<ChatMessage>> _conversations = new();

    private const string SystemPrompt = """
        You are a helpful study assistant. Answer the user's question based ONLY on the 
        provided context. If the context doesn't contain enough information to answer, 
        say so honestly — do not make up information.

        When answering:
        - Be clear and concise
        - Reference specific parts of the source material
        - If the question is about an exam topic, explain it in a way that aids memorization
        - Highlight key terms and concepts

        Context from documents:
        {context}
        """;

    public QueryController(
        IEmbeddingProvider embeddingProvider,
        ILlmProvider llmProvider,
        IVectorStore vectorStore)
    {
        _embeddingProvider = embeddingProvider;
        _llmProvider = llmProvider;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Ask a question against your uploaded documents.
    /// The RAG pipeline retrieves relevant chunks and generates a grounded answer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        // 1. Embed the question
        var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(
            request.Question, cancellationToken);

        // 2. Retrieve relevant chunks
        var retrievedChunks = await _vectorStore.SearchAsync(
            queryEmbedding, topK: 5, cancellationToken);

        if (!retrievedChunks.Any())
        {
            return Ok(new QueryResponse
            {
                Answer = "I couldn't find any relevant information in the uploaded documents. Try uploading more materials or rephrasing your question.",
                Sources = new List<SourceReference>(),
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString()
            });
        }

        // 3. Build context from retrieved chunks
        var context = string.Join("\n\n---\n\n",
            retrievedChunks.Select(c =>
                $"[Source: {c.Chunk.Metadata.GetValueOrDefault("fileName", "unknown")}]\n{c.Chunk.Content}"));

        var prompt = SystemPrompt.Replace("{context}", context);

        // 4. Get or create conversation history
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        if (!_conversations.ContainsKey(conversationId))
            _conversations[conversationId] = new List<ChatMessage>();

        var history = _conversations[conversationId];
        history.Add(new ChatMessage { Role = "user", Content = request.Question });

        // 5. Generate answer
        var answer = await _llmProvider.GenerateCompletionAsync(
            prompt, history, cancellationToken);

        // 6. Store assistant response in history
        history.Add(new ChatMessage { Role = "assistant", Content = answer });

        // 7. Build response with sources
        var response = new QueryResponse
        {
            Answer = answer,
            ConversationId = conversationId,
            Sources = retrievedChunks.Select(c => new SourceReference
            {
                DocumentName = c.Chunk.Metadata.GetValueOrDefault("fileName", "unknown"),
                ChunkContent = c.Chunk.Content.Length > 200
                    ? c.Chunk.Content[..200] + "..."
                    : c.Chunk.Content,
                RelevanceScore = c.Score
            }).ToList()
        };

        return Ok(response);
    }
}
