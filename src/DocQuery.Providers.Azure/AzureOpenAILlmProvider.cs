using DocQuery.Core.Interfaces;

namespace DocQuery.Providers.Azure;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║  YOUR TURN — AI-102 PRACTICE                                    ║
/// ║                                                                  ║
/// ║  Implement this class using the Azure.AI.OpenAI SDK.             ║
/// ║  Use OllamaLlmProvider as your reference pattern.                ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// 
/// STEP-BY-STEP GUIDE:
///
/// 1. Inject AzureOpenAIOptions (same options class from the embedding provider)
///
/// 2. Implement GenerateCompletionAsync:
///    - Create an AzureOpenAIClient with your endpoint + key
///    - Call client.GetChatClient(deploymentName)
///    - Build a list of ChatMessage objects (SystemChatMessage, UserChatMessage, etc.)
///    - Call chatClient.CompleteChatAsync(messages)
///    - Return the response content as a string
///
/// 3. For the conversation history overload:
///    - Map your ChatMessage list to Azure SDK ChatMessage types
///    - "system" → SystemChatMessage
///    - "user" → UserChatMessage  
///    - "assistant" → AssistantChatMessage
///
/// AI-102 EXAM TOPICS THIS COVERS:
///   - Implementing Azure OpenAI chat completions
///   - Configuring system prompts for grounded responses
///   - Understanding token limits and truncation strategies
///   - Content filtering and responsible AI
///   - The difference between completions and chat completions
///
/// BONUS (exam + interview points):
///   - Add a temperature parameter (lower = more deterministic for RAG)
///   - Implement max_tokens to control response length
///   - Add content filter result checking from the response
///
/// DOCS:
///   https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt
///   https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai
/// </summary>
public class AzureOpenAILlmProvider : ILlmProvider
{
    public Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement using Azure.AI.OpenAI SDK
        throw new NotImplementedException(
            "Implement this! See the XML docs above for step-by-step guidance.");
    }

    public Task<string> GenerateCompletionAsync(
        string systemPrompt,
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement with conversation history support
        throw new NotImplementedException(
            "Implement this! Map ChatMessage.Role to Azure SDK message types.");
    }
}
