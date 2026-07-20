using DocQuery.Core.Services;
using Xunit;

namespace DocQuery.Api.Tests;

public class ChunkingServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChunkText_EmptyOrWhitespace_ReturnsNoChunks(string input)
    {
        var service = new ChunkingService();

        Assert.Empty(service.ChunkText(input));
    }

    [Fact]
    public void ChunkText_ShorterThanChunkSize_ReturnsSingleChunk()
    {
        var service = new ChunkingService(chunkSize: 100, chunkOverlap: 10);

        var chunks = service.ChunkText("a short document with few words");

        var chunk = Assert.Single(chunks);
        Assert.Equal("a short document with few words", chunk);
    }

    [Fact]
    public void ChunkText_LongText_SplitsWithOverlap()
    {
        var service = new ChunkingService(chunkSize: 10, chunkOverlap: 2);
        var words = Enumerable.Range(1, 25).Select(i => $"w{i}").ToArray();

        var chunks = service.ChunkText(string.Join(' ', words));

        // Window advances by (size - overlap) = 8 words: positions 0, 8, 16, 24.
        Assert.Equal(4, chunks.Count);
        Assert.Equal(string.Join(' ', words[0..10]), chunks[0]);
        Assert.Equal(string.Join(' ', words[8..18]), chunks[1]);

        // The last two words of a chunk open the next one.
        Assert.StartsWith("w9 w10", chunks[1]);
        Assert.StartsWith("w17 w18", chunks[2]);
    }

    [Fact]
    public void ChunkText_CoversEveryWord()
    {
        var service = new ChunkingService(chunkSize: 10, chunkOverlap: 2);
        var words = Enumerable.Range(1, 53).Select(i => $"w{i}").ToArray();

        var chunks = service.ChunkText(string.Join(' ', words));
        var covered = chunks.SelectMany(c => c.Split(' ')).ToHashSet();

        Assert.All(words, word => Assert.Contains(word, covered));
    }
}