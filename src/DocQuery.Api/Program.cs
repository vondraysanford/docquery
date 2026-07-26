using DocQuery.Api.Services;
using DocQuery.Core.Interfaces;
using DocQuery.Core.Services;
using DocQuery.Providers.Azure;
using DocQuery.Providers.Local;
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000") // React dev server
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

app.MapControllers();

app.Logger.LogInformation(
    "DocQuery starting with {Count} provider profile(s); default: {Default}",
    registry.Profiles.Count, registry.DefaultProfileName);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

// Exposes the implicit Program class to WebApplicationFactory in smoke tests.
public partial class Program { }
