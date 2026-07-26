using DocQuery.Api.Services;
using DocQuery.Providers.Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DocQuery.Api.Controllers;

/// <summary>
/// Reports the configured provider profiles and their live availability, so
/// the UI can offer a selector with unreachable options disabled (and say why).
/// </summary>
[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly ProfileRegistry _registry;
    private readonly IProviderContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AzureOpenAIOptions> _azureOptions;

    public ProvidersController(
        ProfileRegistry registry,
        IProviderContext context,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAIOptions> azureOptions)
    {
        _registry = registry;
        _context = context;
        _httpClientFactory = httpClientFactory;
        _azureOptions = azureOptions;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var checks = _registry.Profiles.Select(async profile =>
        {
            var (available, reason) = await CheckAvailabilityAsync(profile, cancellationToken);
            return new
            {
                name = profile.Name,
                displayName = profile.DisplayName,
                type = profile.Type.ToString(),
                isDefault = profile.Name == _registry.DefaultProfileName,
                isActive = profile.Name == _context.ProfileName,
                available,
                reason,
                hint = profile.Hint,
            };
        });

        return Ok(await Task.WhenAll(checks));
    }

    private async Task<(bool Available, string? Reason)> CheckAvailabilityAsync(
        ProviderProfile profile, CancellationToken cancellationToken)
    {
        if (profile.Type == ProfileType.Azure)
        {
            var options = _azureOptions.Value;
            var configured = !string.IsNullOrWhiteSpace(options.Endpoint)
                && !options.Endpoint.Contains("YOUR_RESOURCE")
                && !string.IsNullOrWhiteSpace(options.ApiKey)
                && options.ApiKey != "YOUR_KEY";
            return configured
                ? (true, null)
                : (false, "Azure credentials are not configured in appsettings.");
        }

        var client = _httpClientFactory.CreateClient("provider-health");
        client.Timeout = TimeSpan.FromSeconds(2);

        // Ollama reachable — and does it actually have this profile's chat model?
        try
        {
            var response = await client.GetAsync($"{profile.Ollama!.BaseUrl.TrimEnd('/')}/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var models = payload.GetProperty("models").EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? "")
                .ToList();

            var wanted = profile.Ollama.ChatModel;
            var hasModel = models.Any(m =>
                m == wanted || (!wanted.Contains(':') && m == $"{wanted}:latest"));
            if (!hasModel)
                return (false, $"Ollama at {profile.Ollama.BaseUrl} is up but model '{wanted}' is not pulled.");
        }
        catch (Exception)
        {
            return (false, $"Ollama unreachable at {profile.Ollama!.BaseUrl}.");
        }

        try
        {
            var response = await client.GetAsync(
                $"{profile.ChromaDb!.BaseUrl.TrimEnd('/')}/api/v2/heartbeat", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            return (false, $"ChromaDB unreachable at {profile.ChromaDb!.BaseUrl}.");
        }

        return (true, null);
    }
}
