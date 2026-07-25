using DocQuery.Core.Models;
using DocQuery.Providers.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Opt-in integration test against a real Azure AI Search service.
/// No-ops when appsettings.json still has placeholder values. Creates the
/// real index on first run; uses a unique documentId per run and cleans up
/// after itself. Indexing is near-real-time, hence the polling.
/// </summary>
public class AzureSearchIntegrationTests
{
    private static AzureSearchOptions? LoadRealOptionsOrNull()
    {
        var appsettingsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "DocQuery.Api", "appsettings.json"));

        if (!File.Exists(appsettingsPath))
            return null;

        var config = new ConfigurationBuilder().AddJsonFile(appsettingsPath).Build();
        var options = config.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>();

        var isPlaceholder = options is null
            || string.IsNullOrWhiteSpace(options.Endpoint)
            || options.Endpoint.Contains("YOUR_RESOURCE")
            || string.IsNullOrWhiteSpace(options.ApiKey)
            || options.ApiKey == "YOUR_KEY";

        return isPlaceholder ? null : options;
    }

    private static float[] MakeVector(int dimensions, int hotIndex)
    {
        var vector = new float[dimensions];
        vector[hotIndex] = 1f;
        return vector;
    }

    [Fact]
    public async Task StoreSearchDelete_RoundTripsAgainstRealService()
    {
        var options = LoadRealOptionsOrNull();
        if (options is null)
            return; // no real Azure Search config on this machine — effectively skipped

        var store = new AzureSearchVectorStore(Options.Create(options));
        var documentId = Guid.NewGuid().ToString();
        var dims = options.VectorDimensions;

        var chunks = new List<DocumentChunk>
        {
            new()
            {
                DocumentId = documentId,
                Content = "integration chunk alpha",
                ChunkIndex = 0,
                Embedding = MakeVector(dims, hotIndex: 0),
                Metadata = new Dictionary<string, string> { ["fileName"] = "integration.txt" },
            },
            new()
            {
                DocumentId = documentId,
                Content = "integration chunk beta",
                ChunkIndex = 1,
                Embedding = MakeVector(dims, hotIndex: 1),
                Metadata = new Dictionary<string, string> { ["fileName"] = "integration.txt" },
            },
        };

        await store.StoreChunksAsync(chunks);

        // Poll until the near-real-time indexing surfaces our chunks (max ~15s).
        List<RetrievedChunk> results = new();
        for (var attempt = 0; attempt < 15; attempt++)
        {
            results = await store.SearchAsync(MakeVector(dims, hotIndex: 0), topK: 3);
            if (results.Any(r => r.Chunk.DocumentId == documentId))
                break;
            await Task.Delay(1000);
        }

        try
        {
            var ours = results.Where(r => r.Chunk.DocumentId == documentId).ToList();
            Assert.NotEmpty(ours);

            // The alpha chunk's vector matches the query exactly — it must rank
            // above beta among our chunks, and carry its metadata through.
            var best = ours.First();
            Assert.Equal("integration chunk alpha", best.Chunk.Content);
            Assert.Equal("integration.txt", best.Chunk.Metadata.GetValueOrDefault("fileName"));
            Assert.True(best.Score > 0);
        }
        finally
        {
            await store.DeleteDocumentAsync(documentId);
        }

        // Deletion is also near-real-time; poll until our chunks are gone.
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var after = await store.SearchAsync(MakeVector(dims, hotIndex: 0), topK: 10);
            if (!after.Any(r => r.Chunk.DocumentId == documentId))
                return; // verified gone
            await Task.Delay(1000);
        }

        Assert.Fail("chunks still retrievable 15s after DeleteDocumentAsync");
    }
}
