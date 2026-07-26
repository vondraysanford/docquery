using DocQuery.Providers.Azure;
using DocQuery.Providers.Local;

namespace DocQuery.Api.Services;

public enum ProfileType { Local, Azure }

/// <summary>
/// One named provider stack the API can route a request to. Local-type
/// profiles carry their own Ollama/ChromaDB settings (so e.g. "Spark" can
/// point at a tunnel with a bigger chat model while sharing the same vector
/// store); Azure-type profiles use the flat DocQuery:Azure:* sections.
/// </summary>
public class ProviderProfile
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required ProfileType Type { get; init; }
    public OllamaOptions? Ollama { get; init; }
    public ChromaDbOptions? ChromaDb { get; init; }
    /// <summary>Optional UX hint, e.g. "~20 s per answer".</summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Identity of the underlying vector store. Profiles sharing a store
    /// (e.g. Local and Spark both using the same ChromaDB) share document
    /// registries, so the UI's document list stays truthful per provider.
    /// </summary>
    public required string StoreKey { get; init; }
}

/// <summary>
/// All configured profiles, built once at startup from DocQuery:Profiles.
/// When no Profiles section exists, synthesizes "Local" and "Azure" profiles
/// from the legacy flat sections so existing configs (and the compose env
/// vars) keep working unchanged.
/// </summary>
public class ProfileRegistry
{
    private readonly Dictionary<string, ProviderProfile> _profiles;

    public IReadOnlyCollection<ProviderProfile> Profiles => _profiles.Values;
    public string DefaultProfileName { get; }

    private ProfileRegistry(Dictionary<string, ProviderProfile> profiles, string defaultProfileName)
    {
        _profiles = profiles;
        DefaultProfileName = defaultProfileName;
    }

    public bool TryGet(string name, out ProviderProfile profile)
        => _profiles.TryGetValue(name, out profile!);

    public ProviderProfile Get(string name) => _profiles[name];

    public static ProfileRegistry FromConfiguration(IConfiguration configuration)
    {
        var baseOllama = new OllamaOptions();
        configuration.GetSection(OllamaOptions.SectionName).Bind(baseOllama);
        var baseChroma = new ChromaDbOptions();
        configuration.GetSection(ChromaDbOptions.SectionName).Bind(baseChroma);

        var profiles = new Dictionary<string, ProviderProfile>(StringComparer.OrdinalIgnoreCase);
        var profilesSection = configuration.GetSection("DocQuery:Profiles");

        if (profilesSection.Exists())
        {
            foreach (var section in profilesSection.GetChildren())
            {
                var name = section.Key;
                if (!Enum.TryParse<ProfileType>(section["Type"], ignoreCase: true, out var type))
                    throw new InvalidOperationException(
                        $"Profile '{name}' has missing or invalid Type — use \"Local\" or \"Azure\".");

                OllamaOptions? ollama = null;
                ChromaDbOptions? chroma = null;
                if (type == ProfileType.Local)
                {
                    // Start from the base sections; profile-level subsections
                    // override only the keys they specify.
                    ollama = new OllamaOptions
                    {
                        BaseUrl = baseOllama.BaseUrl,
                        EmbeddingModel = baseOllama.EmbeddingModel,
                        ChatModel = baseOllama.ChatModel,
                    };
                    section.GetSection("Ollama").Bind(ollama);
                    chroma = new ChromaDbOptions { BaseUrl = baseChroma.BaseUrl };
                    section.GetSection("ChromaDb").Bind(chroma);
                }

                profiles[name] = new ProviderProfile
                {
                    Name = name,
                    DisplayName = section["DisplayName"] ?? name,
                    Type = type,
                    Ollama = ollama,
                    ChromaDb = chroma,
                    Hint = section["Hint"],
                    StoreKey = StoreKeyFor(type, chroma, configuration),
                };
            }
        }
        else
        {
            profiles["Local"] = new ProviderProfile
            {
                Name = "Local",
                DisplayName = "Local",
                Type = ProfileType.Local,
                Ollama = baseOllama,
                ChromaDb = baseChroma,
                StoreKey = StoreKeyFor(ProfileType.Local, baseChroma, configuration),
            };
            profiles["Azure"] = new ProviderProfile
            {
                Name = "Azure",
                DisplayName = "Azure",
                Type = ProfileType.Azure,
                StoreKey = StoreKeyFor(ProfileType.Azure, null, configuration),
            };
        }

        return Finish(profiles, configuration);
    }

    private static string StoreKeyFor(ProfileType type, ChromaDbOptions? chroma, IConfiguration configuration)
        => type == ProfileType.Local
            ? $"chroma:{chroma!.BaseUrl.TrimEnd('/')}"
            : $"azure-search:{configuration["DocQuery:Azure:Search:Endpoint"]}/{configuration["DocQuery:Azure:Search:IndexName"]}";

    private static ProfileRegistry Finish(Dictionary<string, ProviderProfile> profiles, IConfiguration configuration)
    {
        // DefaultProfile wins; the legacy Provider key keeps old configs and
        // the compose DocQuery__Provider env var working.
        var defaultName = configuration["DocQuery:DefaultProfile"]
            ?? configuration["DocQuery:Provider"]
            ?? "Local";

        if (!profiles.TryGetValue(defaultName, out var defaultProfile))
            throw new InvalidOperationException(
                $"Default profile '{defaultName}' is not defined. Configured profiles: {string.Join(", ", profiles.Keys)}.");

        return new ProfileRegistry(profiles, defaultProfile.Name);
    }
}
