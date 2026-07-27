using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DocQuery.Api.Services;
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
    private readonly string _profileName;

    // Session-scoped in-memory conversation store: survives across requests,
    // forgotten on restart. Capped per conversation so long sessions can't
    // outgrow the model's context window (or inflate Azure token costs).
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();
    private const int MaxHistoryMessages = 20; // 10 question/answer exchanges

    // Anyone can mint new conversationIds, so the store must be bounded or a
    // scripted client can grow it without limit. Oldest conversations are
    // evicted once the cap is reached.
    private const int MaxConversations = 500;
    private static readonly ConcurrentQueue<string> _conversationOrder = new();

    // Bounds what a single question can cost: the string is embedded and sent
    // to the chat model, so its length is directly billable.
    private const int MaxQuestionLength = 2000;

    private const string NoResultsAnswer =
        "I couldn't find any relevant information in the uploaded documents. Try uploading more materials or rephrasing your question.";

    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

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

    public QueryController(IProviderContext providers)
    {
        _embeddingProvider = providers.Embeddings;
        _llmProvider = providers.Llm;
        _vectorStore = providers.VectorStore;
        _profileName = providers.ProfileName;
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
        if (request.Question.Length > MaxQuestionLength)
            return BadRequest($"Question is too long (max {MaxQuestionLength} characters).");

        var retrievedChunks = await RetrieveAsync(request.Question, cancellationToken);

        if (!retrievedChunks.Any())
        {
            return Ok(new QueryResponse
            {
                Answer = NoResultsAnswer,
                Sources = new List<SourceReference>(),
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
                Provider = _profileName
            });
        }

        var prompt = BuildPrompt(retrievedChunks);
        var (conversationId, history, historySnapshot) = AppendUserTurn(request);

        var answer = await _llmProvider.GenerateCompletionAsync(
            prompt, historySnapshot, cancellationToken);

        AppendAssistantTurn(history, answer);

        return Ok(new QueryResponse
        {
            Answer = answer,
            ConversationId = conversationId,
            Sources = ToSourceReferences(retrievedChunks),
            Provider = _profileName
        });
    }

    /// <summary>
    /// Streaming variant of Ask, as Server-Sent Events. Event order:
    /// "sources" (citations, available as soon as retrieval completes),
    /// then "token" deltas as the model generates, then "done" with the
    /// conversation id.
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Question is required.", cancellationToken);
            return;
        }
        if (request.Question.Length > MaxQuestionLength)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync($"Question is too long (max {MaxQuestionLength} characters).", cancellationToken);
            return;
        }

        var retrievedChunks = await RetrieveAsync(request.Question, cancellationToken);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no"); // tells nginx not to buffer

        if (!retrievedChunks.Any())
        {
            var fallbackId = request.ConversationId ?? Guid.NewGuid().ToString();
            await WriteEventAsync("sources", new List<SourceReference>(), cancellationToken);
            await WriteEventAsync("token", new { t = NoResultsAnswer }, cancellationToken);
            await WriteEventAsync("done", new { conversationId = fallbackId, provider = _profileName }, cancellationToken);
            return;
        }

        var prompt = BuildPrompt(retrievedChunks);
        var (conversationId, history, historySnapshot) = AppendUserTurn(request);

        // Citations first: retrieval is already done, no reason to make the
        // user wait for generation to see what grounded the answer.
        await WriteEventAsync("sources", ToSourceReferences(retrievedChunks), cancellationToken);

        var fullAnswer = new StringBuilder();
        await foreach (var delta in _llmProvider.GenerateCompletionStreamAsync(
            prompt, historySnapshot, cancellationToken))
        {
            fullAnswer.Append(delta);
            await WriteEventAsync("token", new { t = delta }, cancellationToken);
        }

        AppendAssistantTurn(history, fullAnswer.ToString());

        await WriteEventAsync("done", new { conversationId, provider = _profileName }, cancellationToken);
    }

    private async Task WriteEventAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(payload, SseJsonOptions);
        await Response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task<List<RetrievedChunk>> RetrieveAsync(string question, CancellationToken cancellationToken)
    {
        var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(question, cancellationToken);
        return await _vectorStore.SearchAsync(queryEmbedding, topK: 5, cancellationToken);
    }

    private static string BuildPrompt(List<RetrievedChunk> retrievedChunks)
    {
        var context = string.Join("\n\n---\n\n",
            retrievedChunks.Select(c =>
                $"[Source: {c.Chunk.Metadata.GetValueOrDefault("fileName", "unknown")}]\n{c.Chunk.Content}"));

        return SystemPrompt.Replace("{context}", context);
    }

    private static List<SourceReference> ToSourceReferences(List<RetrievedChunk> retrievedChunks)
        => retrievedChunks.Select(c => new SourceReference
        {
            DocumentName = c.Chunk.Metadata.GetValueOrDefault("fileName", "unknown"),
            ChunkContent = c.Chunk.Content.Length > 200
                ? c.Chunk.Content[..200] + "..."
                : c.Chunk.Content,
            RelevanceScore = c.Score
        }).ToList();

    /// <summary>
    /// Records the user's question in the conversation and returns a snapshot
    /// for the LLM call. The list is locked around mutations so concurrent
    /// requests on the same conversation can't corrupt it.
    /// </summary>
    private static (string ConversationId, List<ChatMessage> History, List<ChatMessage> Snapshot)
        AppendUserTurn(QueryRequest request)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var history = _conversations.GetOrAdd(conversationId, id =>
        {
            _conversationOrder.Enqueue(id);
            while (_conversations.Count >= MaxConversations && _conversationOrder.TryDequeue(out var oldest))
                _conversations.TryRemove(oldest, out _);
            return new List<ChatMessage>();
        });

        List<ChatMessage> snapshot;
        lock (history)
        {
            history.Add(new ChatMessage { Role = "user", Content = request.Question });
            TrimToCap(history);
            snapshot = new List<ChatMessage>(history);
        }

        return (conversationId, history, snapshot);
    }

    private static void AppendAssistantTurn(List<ChatMessage> history, string answer)
    {
        lock (history)
        {
            history.Add(new ChatMessage { Role = "assistant", Content = answer });
            TrimToCap(history);
        }
    }

    private static void TrimToCap(List<ChatMessage> history)
    {
        if (history.Count > MaxHistoryMessages)
            history.RemoveRange(0, history.Count - MaxHistoryMessages);
    }
}