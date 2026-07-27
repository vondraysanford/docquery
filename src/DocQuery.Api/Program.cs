using System.Threading.RateLimiting;
using DocQuery.Api.Services;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Services;
using DocQuery.Providers.Azure;
using DocQuery.Providers.Local;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Provider profiles ---
// Every configured profile (DocQuery:Profiles) gets its provider trio
// registered as keyed services; each request picks a profile via the
// X-DocQuery-Profile header, falling back to DocQuery:DefaultProfile.
var registry = ProfileRegistry.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(registry);

builder.Services.AddSingleton<ChunkingService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProviderContext, HeaderProviderContext>();
builder.Services.AddSingleton<DocumentRegistry>();

// Demo mode: read-only seeded corpus, preset questions, rate limiting.
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));
var demoOptions = builder.Configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();
builder.Services.AddSingleton<CorpusSeeder>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CorpusSeeder>());

if (demoOptions.Enabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            // Health probes must never be throttled — container platforms
            // interpret 429s from /health as "app is down" and restart it.
            if (context.Request.Path.StartsWithSegments("/health"))
                return RateLimitPartition.GetNoLimiter("health");

            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = demoOptions.RateLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                });
        });
    });
}

// Azure options are flat sections shared by all Azure-type profiles.
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection(AzureSearchOptions.SectionName));

foreach (var profile in registry.Profiles)
{
    var key = profile.Name;
    if (profile.Type == ProfileType.Local)
    {
        var ollamaOptions = Options.Create(profile.Ollama!);
        var chromaOptions = Options.Create(profile.ChromaDb!);
        builder.Services.AddKeyedSingleton<IEmbeddingProvider>(key, (sp, _) =>
            new OllamaEmbeddingProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient($"ollama-{key}"), ollamaOptions));
        builder.Services.AddKeyedSingleton<ILlmProvider>(key, (sp, _) =>
            new OllamaLlmProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient($"ollama-{key}"), ollamaOptions));
        builder.Services.AddKeyedSingleton<IVectorStore>(key, (sp, _) =>
            new ChromaVectorStore(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient($"chroma-{key}"), chromaOptions));
    }
    else
    {
        builder.Services.AddKeyedSingleton<IEmbeddingProvider>(key, (sp, _) =>
            new AzureOpenAIEmbeddingProvider(sp.GetRequiredService<IOptions<AzureOpenAIOptions>>()));
        builder.Services.AddKeyedSingleton<ILlmProvider>(key, (sp, _) =>
            new AzureOpenAILlmProvider(sp.GetRequiredService<IOptions<AzureOpenAIOptions>>()));
        builder.Services.AddKeyedSingleton<IVectorStore>(key, (sp, _) =>
            new AzureSearchVectorStore(sp.GetRequiredService<IOptions<AzureSearchOptions>>()));
    }
}

// --- API setup ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allowed browser origins come from config so the deployed UI's domain can
// be added without a code change; localhost:3000 stays the dev default.
var corsOrigins = builder.Configuration.GetSection("DocQuery:Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Reject unknown profile names before any controller runs, so a typo'd
// header is a clean 400 instead of a failed keyed-service resolution.
app.Use(async (context, next) =>
{
    var requested = context.Request.Headers[HeaderProviderContext.HeaderName].FirstOrDefault();
    if (requested is not null && !registry.TryGet(requested, out _))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = $"Unknown provider profile '{requested}'. Configured profiles: {string.Join(", ", registry.Profiles.Select(p => p.Name))}."
        });
        return;
    }
    await next();
});

if (demoOptions.Enabled)
    app.UseRateLimiter();

app.MapControllers();

// Lets the UI adapt to demo mode without hardcoding anything client-side.
app.MapGet("/api/config", (Microsoft.Extensions.Options.IOptions<DemoOptions> demo) => Results.Ok(new
{
    demoMode = demo.Value.Enabled,
    presetQuestions = demo.Value.PresetQuestions,
}));

app.Logger.LogInformation(
    "DocQuery starting with {Count} provider profile(s); default: {Default}; demo mode: {Demo}",
    registry.Profiles.Count, registry.DefaultProfileName, demoOptions.Enabled);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

// Exposes the implicit Program class to WebApplicationFactory in smoke tests.
public partial class Program { }
