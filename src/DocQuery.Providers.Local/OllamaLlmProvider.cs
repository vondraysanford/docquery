using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
        var request = new
        {
            model = _options.ChatModel,
            messages = BuildMessages(systemPrompt, conversationHistory),
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return result.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        List<ChatMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.ChatModel,
            messages = BuildMessages(systemPrompt, conversationHistory),
            stream = true
        };

        // Ollama streams newline-delimited JSON, one object per token batch.
        // ResponseHeadersRead makes deltas available as they arrive instead of
        // buffering the whole body.
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var chunk = JsonSerializer.Deserialize<JsonElement>(line);
            if (chunk.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                var delta = content.GetString();
                if (!string.IsNullOrEmpty(delta))
                    yield return delta;
            }

            if (chunk.TryGetProperty("done", out var done) && done.GetBoolean())
                yield break;
        }
    }

    private static List<object> BuildMessages(string systemPrompt, List<ChatMessage> conversationHistory)
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

        return messages;
    }
}