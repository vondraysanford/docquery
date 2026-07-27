using System.Collections.Concurrent;
using DocQuery.Core.Models;

namespace DocQuery.Api.Services;

/// <summary>
/// In-memory tracking of uploaded/seeded documents, scoped per vector store
/// (see ProviderProfile.StoreKey) so the UI's document list stays truthful
/// per provider. Shared by the documents controller and the corpus seeder.
/// Forgotten on restart — a known limitation; seeded corpora re-register on
/// startup, manual uploads don't.
/// </summary>
public class DocumentRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Document>> _byStore = new();

    public ConcurrentDictionary<string, Document> ForStore(string storeKey)
        => _byStore.GetOrAdd(storeKey, _ => new ConcurrentDictionary<string, Document>());
}
