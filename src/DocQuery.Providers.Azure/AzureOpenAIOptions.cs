namespace DocQuery.Providers.Azure;

public class AzureOpenAIOptions
{
    public const string SectionName = "DocQuery:Azure:OpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    // Azure addresses deployments by the name chosen at deployment time,
    // not by model id — these defaults match the appsettings example.
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";
    public string ChatDeployment { get; set; } = "gpt-5-mini";
}
