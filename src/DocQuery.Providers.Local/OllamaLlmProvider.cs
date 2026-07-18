using System.Net.Http.Json;
using System.Text.Json;
using DocQuery.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DocQuery.Providers.Local;

/// <summary>
/// Sends chat completions to a locally-running Ollama instance.
/// Reference implementation — use this pattern for AzureOpenAILlmProvider.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaLlmProvider(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Local inference can be slow on large models
    }

    public async Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = userMessage }
        };

        return await GenerateCompletionAsync(systemPrompt, messages, cancellationToken);
    }

    public async Task<string> GenerateCompletionAsync(
        string systemPrompt,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        messages.AddRange(conversationHistory.Select(m => (object)new
        {
            role = m.Role,
            content = m.Content
        }));

        var request = new
        {
            model = _options.ChatModel,
            messages,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return result.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
