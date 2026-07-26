using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Verifies the SSE streaming endpoint over the fake providers: event order
/// (sources → tokens → done), that concatenated deltas reproduce the full
/// answer, and that streamed exchanges land in conversation memory.
/// </summary>
public class StreamingTests : IClassFixture<ApiSmokeTests.TestAppFactory>
{
    private readonly HttpClient _client;
    private readonly FakeLlmProvider _llm;

    public StreamingTests(ApiSmokeTests.TestAppFactory factory)
    {
        _client = factory.CreateClient();
        _llm = factory.Services.GetRequiredService<FakeLlmProvider>();
    }

    private async Task EnsureChunksExistAsync()
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("streaming test fixture content")), "file", "streaming-fixture.txt" },
        };
        (await _client.PostAsync("/api/documents/upload", content)).EnsureSuccessStatusCode();
    }

    private static List<(string EventType, JsonElement Data)> ParseSse(string body)
    {
        var events = new List<(string, JsonElement)>();
        foreach (var frame in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? eventType = null;
            string? data = null;
            foreach (var line in frame.Split('\n'))
            {
                if (line.StartsWith("event: ")) eventType = line["event: ".Length..];
                else if (line.StartsWith("data: ")) data = line["data: ".Length..];
            }
            if (eventType is not null && data is not null)
                events.Add((eventType, JsonSerializer.Deserialize<JsonElement>(data)));
        }
        return events;
    }

    [Fact]
    public async Task Stream_EmitsSourcesThenTokensThenDone_ReassemblingFullAnswer()
    {
        await EnsureChunksExistAsync();

        var response = await _client.PostAsJsonAsync("/api/query/stream", new { question = "stream me an answer" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = ParseSse(await response.Content.ReadAsStringAsync());

        Assert.Equal("sources", events.First().EventType);
        Assert.True(events.First().Data.GetArrayLength() >= 1, "sources event should carry citations");

        Assert.Equal("done", events.Last().EventType);
        Assert.False(string.IsNullOrEmpty(events.Last().Data.GetProperty("conversationId").GetString()));

        var tokens = events.Where(e => e.EventType == "token").ToList();
        Assert.True(tokens.Count > 1, "expected multiple token deltas, got a single lump");
        var reassembled = string.Concat(tokens.Select(t => t.Data.GetProperty("t").GetString()));
        Assert.Equal(FakeLlmProvider.CannedAnswer, reassembled);
    }

    [Fact]
    public async Task Stream_EmptyQuestion_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/query/stream", new { question = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stream_RecordsExchangeInConversationMemory()
    {
        await EnsureChunksExistAsync();

        var first = await _client.PostAsJsonAsync("/api/query/stream", new { question = "streamed opening question" });
        var conversationId = ParseSse(await first.Content.ReadAsStringAsync())
            .Last().Data.GetProperty("conversationId").GetString();

        await _client.PostAsJsonAsync("/api/query/stream", new { question = "streamed follow-up", conversationId });

        var delivered = _llm.LastConversation;
        Assert.NotNull(delivered);
        var openingIndex = delivered.FindIndex(m => m.Role == "user" && m.Content == "streamed opening question");
        var answerIndex = delivered.FindIndex(m => m.Role == "assistant" && m.Content == FakeLlmProvider.CannedAnswer);
        var followUpIndex = delivered.FindIndex(m => m.Role == "user" && m.Content == "streamed follow-up");

        Assert.True(openingIndex >= 0, "opening question missing from history");
        Assert.True(answerIndex > openingIndex, "streamed answer missing from history or out of order");
        Assert.True(followUpIndex > answerIndex, "follow-up missing or out of order");
    }
}