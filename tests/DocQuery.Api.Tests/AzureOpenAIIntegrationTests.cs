using DocQuery.Providers.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Opt-in integration tests against a real Azure OpenAI deployment.
/// They read the gitignored appsettings.json; when it's absent or still has
/// placeholder values, they no-op so `dotnet test` stays green on machines
/// without an Azure subscription. Each run costs a fraction of a cent.
/// </summary>
public class AzureOpenAIIntegrationTests
{
    private static AzureOpenAIOptions? LoadRealOptionsOrNull()
    {
        var appsettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "DocQuery.Api", "appsettings.json"));

        if (!File.Exists(appsettingsPath))
            return null;

        // Env vars layer over the file so scenarios like MaxOutputTokens can
        // be exercised against the live service without editing config.
        var config = new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath)
            .AddEnvironmentVariables()
            .Build();
        var options = config.GetSection(AzureOpenAIOptions.SectionName).Get<AzureOpenAIOptions>();

        var isPlaceholder = options is null
            || string.IsNullOrWhiteSpace(options.Endpoint)
            || options.Endpoint.Contains("YOUR_RESOURCE")
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || options.ApiKey == "YOUR_KEY";

        return isPlaceholder ? null : options;
    }

    [Fact]
    public async Task EmbeddingProvider_EmbedsSingleAndBatch()
    {
        var options = LoadRealOptionsOrNull();
        if (options is null)
            return; // no real Azure config on this machine — effectively skipped

        var provider = new AzureOpenAIEmbeddingProvider(Options.Create(options));

        var embedding = await provider.GenerateEmbeddingAsync("DocQuery integration test");
        Assert.True(embedding.Length >= 256, $"suspiciously short embedding: {embedding.Length}");

        var batch = await provider.GenerateEmbeddingsAsync(new List<string> { "first text", "second text" });
        Assert.Equal(2, batch.Count);
        Assert.All(batch, vector => Assert.Equal(embedding.Length, vector.Length));
    }

    [Fact]
    public async Task LlmProvider_CompletesChatWithHistory()
    {
        var options = LoadRealOptionsOrNull();
        if (options is null)
            return; // no real Azure config on this machine — effectively skipped

        var provider = new AzureOpenAILlmProvider(Options.Create(options));

        var answer = await provider.GenerateCompletionAsync(
            "You are a test harness. Answer in as few words as possible.",
            "What is 2 + 2?");

        Assert.False(string.IsNullOrWhiteSpace(answer));
        Assert.Contains("4", answer);
    }
}
