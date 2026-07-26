using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocQuery.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Verifies per-request provider routing: the X-DocQuery-Profile header
/// selects which keyed provider trio serves a request, unknown profiles are
/// rejected, and /api/providers reports the configured profiles.
/// </summary>
public class ProviderRoutingTests : IClassFixture<ApiSmokeTests.TestAppFactory>
{
    private readonly ApiSmokeTests.TestAppFactory _factory;
    private readonly HttpClient _client;

    public ProviderRoutingTests(ApiSmokeTests.TestAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithProfile(HttpRequestMessage request, string? profile)
    {
        if (profile is not null)
            request.Headers.Add(HeaderProviderContext.HeaderName, profile);
        return request;
    }

    private async Task UploadAsync(string fileName, string? profile)
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes($"routing fixture for {fileName}")), "file", fileName },
        };
        var request = WithProfile(new HttpRequestMessage(HttpMethod.Post, "/api/documents/upload"), profile);
        request.Content = content;
        (await _client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> AskAsync(string question, string? profile)
    {
        var request = WithProfile(new HttpRequestMessage(HttpMethod.Post, "/api/query"), profile);
        request.Content = JsonContent.Create(new { question });
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Header_RoutesRequestToThatProfilesProviders()
    {
        var localLlm = _factory.Services.GetRequiredService<FakeLlmProvider>();
        var azureLlm = _factory.Services.GetRequiredKeyedService<FakeLlmProvider>("Azure");
        var azureStore = _factory.Services.GetRequiredKeyedService<FakeVectorStore>("Azure");

        await UploadAsync("azure-routed.txt", "Azure");
        Assert.NotEmpty(azureStore.StoredChunks); // upload landed in the Azure profile's store

        var azureAnswer = await AskAsync("routed question for azure?", "Azure");
        Assert.Equal("Azure", azureAnswer.GetProperty("provider").GetString());
        Assert.NotNull(azureLlm.LastConversation);
        Assert.Contains(azureLlm.LastConversation!, m => m.Content == "routed question for azure?");
        Assert.DoesNotContain(localLlm.LastConversation ?? new(), m => m.Content == "routed question for azure?");

        await UploadAsync("default-routed.txt", profile: null);
        var defaultAnswer = await AskAsync("routed question for default?", profile: null);
        Assert.Equal("Local", defaultAnswer.GetProperty("provider").GetString());
        Assert.Contains(localLlm.LastConversation!, m => m.Content == "routed question for default?");
    }

    [Fact]
    public async Task DocumentList_IsScopedPerProviderStore()
    {
        await UploadAsync("azure-only.txt", "Azure");

        var azureListRequest = WithProfile(new HttpRequestMessage(HttpMethod.Get, "/api/documents"), "Azure");
        var azureList = await (await _client.SendAsync(azureListRequest)).Content.ReadAsStringAsync();
        Assert.Contains("azure-only.txt", azureList);

        var defaultList = await _client.GetStringAsync("/api/documents");
        Assert.DoesNotContain("azure-only.txt", defaultList);
    }

    [Fact]
    public async Task UnknownProfile_IsRejectedWith400()
    {
        var request = WithProfile(new HttpRequestMessage(HttpMethod.Post, "/api/query"), "Mainframe");
        request.Content = JsonContent.Create(new { question = "hello?" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Mainframe", body);
    }

    [Fact]
    public async Task ProvidersEndpoint_ReportsConfiguredProfiles()
    {
        var response = await _client.GetAsync("/api/providers");
        response.EnsureSuccessStatusCode();
        var profiles = await response.Content.ReadFromJsonAsync<JsonElement>();

        var names = profiles.EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("Local", names);
        Assert.Contains("Azure", names);

        var local = profiles.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Local");
        Assert.True(local.GetProperty("isDefault").GetBoolean());
        // DisplayName and availability depend on the machine's config and
        // running services — assert shape, not machine-specific values.
        Assert.False(string.IsNullOrEmpty(local.GetProperty("displayName").GetString()));
        Assert.True(local.TryGetProperty("available", out _));
    }
}
