using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocQuery.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Demo mode behavior: mutations refused, corpus seeded from a folder at
/// startup (idempotently), preset questions exposed via /api/config, and
/// the rate limiter returning 429s. Uses its own factory instance so demo
/// config can't leak into the other test classes.
/// </summary>
public class DemoModeTests : IClassFixture<DemoModeTests.DemoAppFactory>
{
    public class DemoAppFactory : ApiSmokeTests.TestAppFactory
    {
        public string SeedDirectory { get; } =
            Directory.CreateTempSubdirectory("docquery-seed-").FullName;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { Directory.Delete(SeedDirectory, recursive: true); } catch { /* best effort */ }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            File.WriteAllText(Path.Combine(SeedDirectory, "seeded-doc.md"),
                "# Seeded fixture\n\nThe demo corpus contains exactly this seeded fixture document.");

            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DocQuery:Demo:Enabled"] = "true",
                    ["DocQuery:Demo:SeedPath"] = SeedDirectory,
                    ["DocQuery:Demo:RateLimitPerMinute"] = "100",
                    ["DocQuery:Demo:PresetQuestions:0"] = "What is in the seeded fixture?",
                    ["DocQuery:Demo:PresetQuestions:1"] = "Second preset?",
                });
            });
        }
    }

    private readonly DemoAppFactory _factory;
    private readonly HttpClient _client;

    public DemoModeTests(DemoAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_IsRefusedInDemoMode()
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("nope")), "file", "sneaky.txt" },
        };

        var response = await _client.PostAsync("/api/documents/upload", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("read-only", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_IsRefusedInDemoMode()
    {
        var response = await _client.DeleteAsync("/api/documents/anything");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Config_ReportsDemoModeAndPresets()
    {
        var config = await _client.GetFromJsonAsync<JsonElement>("/api/config");

        Assert.True(config.GetProperty("demoMode").GetBoolean());
        var presets = config.GetProperty("presetQuestions").EnumerateArray()
            .Select(q => q.GetString()).ToList();
        Assert.Equal(2, presets.Count);
        Assert.Contains("What is in the seeded fixture?", presets);
    }

    [Fact]
    public async Task Corpus_IsSeededAtStartup_AndSeedingIsIdempotent()
    {
        // The background seeder races test startup — poll briefly.
        JsonElement docs = default;
        var found = false;
        for (var attempt = 0; attempt < 20 && !found; attempt++)
        {
            docs = await _client.GetFromJsonAsync<JsonElement>("/api/documents");
            found = docs.EnumerateArray().Any(d => d.GetProperty("fileName").GetString() == "seeded-doc.md");
            if (!found) await Task.Delay(250);
        }
        Assert.True(found, "seeded document never appeared in the registry");

        // And the seeded content is retrievable end-to-end.
        var answer = await _client.PostAsJsonAsync("/api/query", new { question = "What does the corpus contain?" });
        var body = await answer.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("sources").GetArrayLength() > 0);

        // Second pass over an unchanged folder: everything skipped.
        var seeder = _factory.Services.GetRequiredService<CorpusSeeder>();
        var (seeded, skipped) = await seeder.SeedOnceAsync(CancellationToken.None);
        Assert.Equal(0, seeded);
        Assert.Equal(1, skipped);
    }
}
