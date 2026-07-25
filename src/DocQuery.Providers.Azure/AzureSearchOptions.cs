namespace DocQuery.Providers.Azure;

public class AzureSearchOptions
{
    public const string SectionName = "DocQuery:Azure:Search";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = "docquery-index";

    // Must match the embedding model's output size (text-embedding-3-small: 1536).
    public int VectorDimensions { get; set; } = 1536;
}
