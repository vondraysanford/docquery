namespace DocQuery.Core.Interfaces;

/// <summary>
/// Sends prompts to a large language model and returns completions.
/// 
/// Implement this interface for each provider:
///   - Local: OllamaLlmProvider (done — see DocQuery.Providers.Local)
///   - Azure: AzureOpenAILlmProvider (YOUR TURN — AI-102 practice!)
///
/// AI-102 study notes:
///   - Azure OpenAI chat completions use the /chat/completions endpoint
///   - You'll need: endpoint, API key, and deployment name
///   - Consider implementing content filtering (Azure AI Content Safety)
///   - SDK: Azure.AI.OpenAI NuGet package
///   - Docs: https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generate a completion from a system prompt and user message.
    /// The system prompt sets the behavior; the user message contains the question + retrieved context.
    /// </summary>
    Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a completion with conversation history for follow-up questions.
    /// </summary>
    Task<string> GenerateCompletionAsync(
        string systemPrompt,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A message in a conversation. Maps to the standard chat completion format
/// used by both Ollama and Azure OpenAI.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user"; // "system", "user", or "assistant"
    public string Content { get; set; } = string.Empty;
}
