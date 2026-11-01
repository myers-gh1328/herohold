namespace Herohold.Core;

public sealed class WorkspaceConflictException(string message) : InvalidOperationException(message);

public sealed record WorkspaceSummary(string Id, string Name, string Species, string ClassName, int Level, long Revision);

/// <summary>Versioned character workspaces. A save succeeds only against the revision it was loaded at.</summary>
public interface IWorkspaceStore
{
    /// <summary>The library: one line per stored character, without recalculating any of them.</summary>
    Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default);
    Task<WorkspaceSnapshot?> LoadAsync(string id, CancellationToken ct = default);
    Task<WorkspaceSnapshot> SaveAsync(WorkspaceSnapshot snapshot, long expectedRevision, CancellationToken ct = default);
    /// <summary>Rejects a workspace this store could not save, without saving anything.</summary>
    void Validate(WorkspaceSnapshot snapshot);
    /// <summary>The owner's homebrew items, shared by all of their characters. A store without a catalog has none.</summary>
    Task<IReadOnlyList<CatalogItem>> HomebrewItemsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogItem>>([]);
}
