using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using DocQuery.Core.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using CoreChatMessage = DocQuery.Core.Interfaces.ChatMessage;

namespace DocQuery.Providers.Azure;

/// <summary>
/// Chat completions via an Azure OpenAI deployment, behind the same
/// ILlmProvider contract as the Ollama implementation. Core's role strings
/// ("user"/"assistant") map onto the SDK's typed message classes.
/// </summary>
public class AzureOpenAILlmProvider : ILlmProvider
{
    private readonly ChatClient _client;

    public AzureOpenAILlmProvider(IOptions<AzureOpenAIOptions> options)
    {
        var config = options.Value;
        var azureClient = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new AzureKeyCredential(config.ApiKey));
        _client = azureClient.GetChatClient(config.ChatDeployment);
    }

    public Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var singleTurn = new List<CoreChatMessage>
        {
            new() { Role = "user", Content = userMessage }
        };
        return GenerateCompletionAsync(systemPrompt, singleTurn, cancellationToken);
    }

    public async Task<string> GenerateCompletionAsync(
        string systemPrompt,
        List<CoreChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(systemPrompt, conversationHistory);
        var completion = await _client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return completion.Value.Content[0].Text;
    }

    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        List<CoreChatMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(systemPrompt, conversationHistory);

        await foreach (var update in _client.CompleteChatStreamingAsync(
            messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }

    private static List<OpenAI.Chat.ChatMessage> BuildMessages(
        string systemPrompt, List<CoreChatMessage> conversationHistory)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        foreach (var message in conversationHistory)
        {
            messages.Add(message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? new AssistantChatMessage(message.Content)
                : new UserChatMessage(message.Content));
        }

        return messages;
    }
}