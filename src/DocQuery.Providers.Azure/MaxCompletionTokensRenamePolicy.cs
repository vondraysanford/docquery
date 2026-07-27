using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;

namespace DocQuery.Providers.Azure;

/// <summary>
/// Renames "max_tokens" to "max_completion_tokens" in outgoing chat
/// completion requests. Azure.AI.OpenAI 2.1.0 serializes the token cap under
/// the legacy name, which reasoning-family models reject, and the SDK's own
/// opt-in switch throws on construction. Remove once a fixed SDK ships.
/// </summary>
internal class MaxCompletionTokensRenamePolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
    }

    private static void Rewrite(PipelineMessage message)
    {
        if (message.Request?.Content is null)
            return;
        if (message.Request.Uri?.AbsolutePath.Contains("/chat/completions") != true)
            return;

        using var buffer = new MemoryStream();
        message.Request.Content.WriteTo(buffer);
        var json = Encoding.UTF8.GetString(buffer.ToArray());
        if (!json.Contains("\"max_tokens\""))
            return;

        json = json.Replace("\"max_tokens\":", "\"max_completion_tokens\":");
        message.Request.Content = BinaryContent.Create(BinaryData.FromString(json));
    }
}
