namespace DocQuery.Providers.Local;

public class ChromaDbOptions
{
    public const string SectionName = "DocQuery:ChromaDb";

    public string BaseUrl { get; set; } = "http://localhost:8000";
}
