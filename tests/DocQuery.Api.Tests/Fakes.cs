using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;

namespace DocQuery.Api.Tests;

/// <summary>
/// Deterministic in-memory stand-ins for the external providers, so smoke
/// tests run without Ollama, ChromaDB, or Docker.
/// </summary>
public class FakeEmbeddingProvider : IEmbeddingProvider
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(new float[] { text.Length, 1f, 0f, 0f });

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();
        foreach (var text in texts)
            embeddings.Add(await GenerateEmbeddingAsync(text, cancellationToken));
        return embeddings;
    }
}

public class FakeVectorStore : IVectorStore
{
    private readonly List<DocumentChunk> _chunks = new();

    public IReadOnlyList<DocumentChunk> StoredChunks => _chunks;

    public Task StoreChunksAsync(List<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        _chunks.AddRange(chunks);
        return Task.CompletedTask;
    }

    public Task<List<RetrievedChunk>> SearchAsync(float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default)
    {
        var results = _chunks
            .Take(topK)
            .Select(chunk => new RetrievedChunk { Chunk = chunk, Score = 0.9 })
            .ToList();
        return Task.FromResult(results);
    }

    public Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _chunks.RemoveAll(chunk => chunk.DocumentId == documentId);
        return Task.CompletedTask;
    }
}

public class FakeLlmProvider : ILlmProvider
{
    public const string CannedAnswer = "This is a canned answer grounded in the provided context.";

    /// <summary>Deep copy of the conversation received on the most recent call.</summary>
    public List<ChatMessage>? LastConversation { get; private set; }

    public Task<string> GenerateCompletionAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        => GenerateCompletionAsync(systemPrompt,
            new List<ChatMessage> { new() { Role = "user", Content = userMessage } },
            cancellationToken);

    public Task<string> GenerateCompletionAsync(string systemPrompt, List<ChatMessage> conversationHistory, CancellationToken cancellationToken = default)
    {
        LastConversation = conversationHistory
            .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
            .ToList();
        return Task.FromResult(CannedAnswer);
    }

    // Streams the canned answer in small slices; concatenating every delta
    // reproduces CannedAnswer exactly, mirroring the real providers' contract.
    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        List<ChatMessage> conversationHistory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastConversation = conversationHistory
            .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
            .ToList();

        const int sliceSize = 15;
        for (var i = 0; i < CannedAnswer.Length; i += sliceSize)
        {
            yield return CannedAnswer.Substring(i, Math.Min(sliceSize, CannedAnswer.Length - i));
            await Task.Yield();
        }
    }
}