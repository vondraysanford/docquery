using DocQuery.Core.Interfaces;
using DocQuery.Core.Services;
using DocQuery.Providers.Azure;
using DocQuery.Providers.Local;

var builder = WebApplication.CreateBuilder(args);

// --- Provider configuration ---
// Change "Provider" in appsettings.json to switch between "Local" and "Azure"
var provider = builder.Configuration.GetValue<string>("DocQuery:Provider") ?? "Local";

builder.Services.AddSingleton<ChunkingService>();

if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    // Local mode: Ollama + ChromaDB
    builder.Services.Configure<OllamaOptions>(
        builder.Configuration.GetSection(OllamaOptions.SectionName));
    builder.Services.Configure<ChromaDbOptions>(
        builder.Configuration.GetSection(ChromaDbOptions.SectionName));

    builder.Services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>();
    builder.Services.AddHttpClient<ILlmProvider, OllamaLlmProvider>();
    builder.Services.AddHttpClient<IVectorStore, ChromaVectorStore>();
}
else if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
{
    // Azure mode: Azure OpenAI + Azure AI Search
    builder.Services.Configure<AzureOpenAIOptions>(
        builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
    builder.Services.Configure<AzureSearchOptions>(
        builder.Configuration.GetSection(AzureSearchOptions.SectionName));

    builder.Services.AddSingleton<IEmbeddingProvider, AzureOpenAIEmbeddingProvider>();
    builder.Services.AddSingleton<ILlmProvider, AzureOpenAILlmProvider>();
    builder.Services.AddSingleton<IVectorStore, AzureSearchVectorStore>();
}
else
{
    throw new InvalidOperationException($"Unknown provider: {provider}. Use 'Local' or 'Azure'.");
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
app.MapControllers();

app.Logger.LogInformation("DocQuery starting with {Provider} provider", provider);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

// Exposes the implicit Program class to WebApplicationFactory in smoke tests.
public partial class Program { }
