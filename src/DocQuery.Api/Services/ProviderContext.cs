using DocQuery.Core.Interfaces;

namespace DocQuery.Api.Services;

/// <summary>
/// The provider trio the current request should use, resolved per request
/// from the X-DocQuery-Profile header (falling back to the default profile).
/// Controllers depend on this instead of on the providers directly.
/// </summary>
public interface IProviderContext
{
    string ProfileName { get; }
    string ProfileDisplayName { get; }
    IEmbeddingProvider Embeddings { get; }
    ILlmProvider Llm { get; }
    IVectorStore VectorStore { get; }
}

public class HeaderProviderContext : IProviderContext
{
    public const string HeaderName = "X-DocQuery-Profile";

    private readonly IServiceProvider _services;
    private readonly ProviderProfile _profile;

    public HeaderProviderContext(
        IHttpContextAccessor httpContextAccessor,
        ProfileRegistry registry,
        IServiceProvider services)
    {
        _services = services;

        // Unknown names are rejected with a 400 by middleware before any
        // controller runs, so resolution here can assume validity.
        var requested = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        _profile = requested is not null && registry.TryGet(requested, out var profile)
            ? profile
            : registry.Get(registry.DefaultProfileName);
    }

    public string ProfileName => _profile.Name;
    public string ProfileDisplayName => _profile.DisplayName;

    public IEmbeddingProvider Embeddings
        => _services.GetRequiredKeyedService<IEmbeddingProvider>(_profile.Name);
    public ILlmProvider Llm
        => _services.GetRequiredKeyedService<ILlmProvider>(_profile.Name);
    public IVectorStore VectorStore
        => _services.GetRequiredKeyedService<IVectorStore>(_profile.Name);
}
