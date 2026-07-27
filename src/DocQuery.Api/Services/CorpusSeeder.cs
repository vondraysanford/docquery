using System.Security.Cryptography;
using System.Text;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Models;
using DocQuery.Core.Services;

namespace DocQuery.Api.Services;

public class DemoOptions
{
    public const string SectionName = "DocQuery:Demo";

    public bool Enabled { get; set; }
    /// <summary>Folder of documents to seed at startup; relative paths resolve against the content root.</summary>
    public string SeedPath { get; set; } = "../../docs/samples";
    public string[] PresetQuestions { get; set; } = Array.Empty<string>();
    /// <summary>Requests allowed per client per minute when demo mode is on.</summary>
    public int RateLimitPerMinute { get; set; } = 20;
}

/// <summary>
/// Seeds the default profile's vector store from a folder at startup.
/// Deterministic document ids + content fingerprints make it idempotent:
/// unchanged files are skipped, edited files are deleted and re-ingested.
/// Runs in the background after the app is up and tolerates providers being
/// down — the API must boot regardless.
/// </summary>
public class CorpusSeeder : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ProfileRegistry _registry;
    private readonly DocumentRegistry _documents;
    private readonly ChunkingService _chunkingService;
    private readonly DemoOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CorpusSeeder> _logger;

    public CorpusSeeder(
        IServiceProvider services,
        ProfileRegistry registry,
        DocumentRegistry documents,
        ChunkingService chunkingService,
        Microsoft.Extensions.Options.IOptions<DemoOptions> options,
        IHostEnvironment environment,
        ILogger<CorpusSeeder> logger)
    {
        _services = services;
        _registry = registry;
        _documents = documents;
        _chunkingService = chunkingService;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            var (seeded, skipped) = await SeedOnceAsync(stoppingToken);
            _logger.LogInformation(
                "Corpus seeding complete: {Seeded} document(s) ingested, {Skipped} unchanged", seeded, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Corpus seeding failed — the demo store may be empty");
        }
    }

    /// <summary>One idempotent seeding pass; separated from the host loop so tests can call it directly.</summary>
    public async Task<(int Seeded, int Skipped)> SeedOnceAsync(CancellationToken cancellationToken)
    {
        var path = Path.IsPathRooted(_options.SeedPath)
            ? _options.SeedPath
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.SeedPath));

        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Seed path {Path} does not exist; nothing to seed", path);
            return (0, 0);
        }

        var profile = _registry.Get(_registry.DefaultProfileName);
        var embeddings = _services.GetRequiredKeyedService<IEmbeddingProvider>(profile.Name);
        var vectorStore = _services.GetRequiredKeyedService<IVectorStore>(profile.Name);
        var registry = _documents.ForStore(profile.StoreKey);

        int seeded = 0, skipped = 0;
        foreach (var file in Directory.EnumerateFiles(path).OrderBy(f => f))
        {
            if (!DocumentTextExtractor.IsSupported(file))
                continue;

            var fileName = Path.GetFileName(file);
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var documentId = $"seed-{Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace('.', '-')}";
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

            var stored = await vectorStore.GetDocumentFingerprintAsync(documentId, cancellationToken);
            if (stored == fingerprint)
            {
                skipped++;
                RegisterDocument(registry, documentId, fileName, content);
                continue;
            }

            if (stored is not null)
                await vectorStore.DeleteDocumentAsync(documentId, cancellationToken);

            var chunkTexts = _chunkingService.ChunkText(content);
            var vectors = await embeddings.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);
            var chunks = chunkTexts.Select((text, i) => new DocumentChunk
            {
                Id = $"{documentId}-{i}",
                DocumentId = documentId,
                Content = text,
                ChunkIndex = i,
                Embedding = vectors[i],
                Metadata = new Dictionary<string, string>
                {
                    ["fileName"] = fileName,
                    ["contentHash"] = fingerprint,
                },
            }).ToList();

            await vectorStore.StoreChunksAsync(chunks, cancellationToken);
            RegisterDocument(registry, documentId, fileName, content);
            seeded++;
        }

        return (seeded, skipped);
    }

    private static void RegisterDocument(
        System.Collections.Concurrent.ConcurrentDictionary<string, Document> registry,
        string documentId, string fileName, string content)
    {
        registry[documentId] = new Document
        {
            Id = documentId,
            FileName = fileName,
            Content = content,
        };
    }
}
