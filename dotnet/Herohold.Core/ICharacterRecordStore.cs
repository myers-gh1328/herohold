namespace Herohold.Core;

/// <summary>Stored character records. A save succeeds only against the revision it was loaded at and returns the next revision.</summary>
public interface ICharacterRecordStore
{
    Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default);
    Task<CharacterRecord?> LoadAsync(string id, CancellationToken ct = default);
    Task<CharacterRecord> SaveAsync(CharacterRecord record, long expectedRevision, CancellationToken ct = default);
}
