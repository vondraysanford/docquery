namespace DocQuery.Core.Services;

/// <summary>
/// Splits documents into smaller chunks for embedding and retrieval.
/// 
/// Chunking strategy has a huge impact on RAG quality — often more than model choice.
/// This uses a simple sliding window approach. Consider experimenting with:
///   - Sentence-based splitting
///   - Semantic chunking (split at topic boundaries)
///   - Recursive character splitting (like LangChain)
/// </summary>
public class ChunkingService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public ChunkingService(int chunkSize = 500, int chunkOverlap = 50)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    /// <summary>
    /// Split text into overlapping chunks.
    /// Overlap ensures context isn't lost at chunk boundaries.
    /// </summary>
    public List<string> ChunkText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= _chunkSize)
        {
            chunks.Add(string.Join(' ', words));
            return chunks;
        }

        var position = 0;
        while (position < words.Length)
        {
            var end = Math.Min(position + _chunkSize, words.Length);
            var chunk = string.Join(' ', words[position..end]);
            chunks.Add(chunk.Trim());

            position += _chunkSize - _chunkOverlap;
        }

        return chunks;
    }
}
