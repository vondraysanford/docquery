using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Verifies session-scoped conversation memory: follow-up questions with the
/// same conversationId deliver prior turns to the LLM, new conversations
/// start clean, and history is capped. Uses the captured conversations on
/// FakeLlmProvider — no live services involved.
/// </summary>
public class ConversationMemoryTests : IClassFixture<ApiSmokeTests.TestAppFactory>
{
    private readonly HttpClient _client;
    private readonly FakeLlmProvider _llm;

    public ConversationMemoryTests(ApiSmokeTests.TestAppFactory factory)
    {
        _client = factory.CreateClient();
        _llm = factory.Services.GetRequiredService<FakeLlmProvider>();
    }

    private async Task EnsureChunksExistAsync()
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("memory test fixture content")), "file", "memory-fixture.txt" },
        };
        (await _client.PostAsync("/api/documents/upload", content)).EnsureSuccessStatusCode();
    }

    private async Task<(string ConversationId, JsonElement Body)> AskAsync(string question, string? conversationId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/query", new { question, conversationId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("conversationId").GetString()!, body);
    }

    [Fact]
    public async Task FollowUp_SameConversation_DeliversPriorTurnsToLlm()
    {
        await EnsureChunksExistAsync();

        var (conversationId, _) = await AskAsync("what is the first fact?");
        await AskAsync("and a follow-up about that?", conversationId);

        var delivered = _llm.LastConversation;
        Assert.NotNull(delivered);

        // The LLM must see: first question, its own prior answer, follow-up — in order.
        var firstIndex = delivered.FindIndex(m => m.Role == "user" && m.Content == "what is the first fact?");
        var answerIndex = delivered.FindIndex(m => m.Role == "assistant" && m.Content == FakeLlmProvider.CannedAnswer);
        var followUpIndex = delivered.FindIndex(m => m.Role == "user" && m.Content == "and a follow-up about that?");

        Assert.True(firstIndex >= 0, "first question missing from delivered history");
        Assert.True(answerIndex > firstIndex, "prior assistant answer missing or out of order");
        Assert.True(followUpIndex > answerIndex, "follow-up missing or out of order");
    }

    [Fact]
    public async Task NewConversation_StartsWithCleanHistory()
    {
        await EnsureChunksExistAsync();

        await AskAsync("question in some other conversation");
        var (_, _) = await AskAsync("a brand new conversation's only question");

        var delivered = _llm.LastConversation;
        Assert.NotNull(delivered);
        var onlyMessage = Assert.Single(delivered);
        Assert.Equal("user", onlyMessage.Role);
        Assert.Equal("a brand new conversation's only question", onlyMessage.Content);
    }

    [Fact]
    public async Task LongConversation_HistoryIsCapped()
    {
        await EnsureChunksExistAsync();

        var (conversationId, _) = await AskAsync("exchange 0");
        for (var i = 1; i < 15; i++)
            await AskAsync($"exchange {i}", conversationId);

        var delivered = _llm.LastConversation;
        Assert.NotNull(delivered);

        // 15 exchanges = 30 messages uncapped; the cap keeps at most 20 and
        // always retains the newest turn.
        Assert.True(delivered.Count <= 20, $"history not capped: {delivered.Count} messages delivered");
        Assert.Equal("exchange 14", delivered.Last(m => m.Role == "user").Content);
        Assert.DoesNotContain(delivered, m => m.Content == "exchange 0");
    }
}
