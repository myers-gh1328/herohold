using System.Text.Json;
using System.Text.Json.Serialization;
using Herohold.Core;

namespace Herohold.Web.Services;

/// <summary>Splits a live character into its stored record, and seeds the rules with a record when loading.</summary>
public static class CharacterRecords
{
    public static CharacterRecord FromState(CharacterState c, long revision, string ownerId = CharacterRecord.DefaultOwnerId)
    {
        if (c.Build is null) throw new ArgumentException("This character has no build to store.");
        return new CharacterRecord
        {
            Id = c.Id, OwnerId = ownerId, Revision = revision,
            Build = WorkspaceProjection.BuildOf(c),
            Facts = new CharacterFacts
            {
                CurrentHp = c.CurrentHp, TemporaryHp = c.TemporaryHp, Turn = c.Turn, Exhaustion = c.Exhaustion,
                Equipment = c.Equipment, Notes = c.Notes, Effects = c.Effects, Combat = c.Combat, Sheet = c.Sheet, KnownTarget = c.KnownTarget,
                Defenses = [.. c.Defenses.Where(d => d.Source != "Character build")],
                Resources = [.. c.Resources.Select(r => r.SourceKind == "manual" ? r : new ResourcePool { Id = r.Id, SourceKind = r.SourceKind, Remaining = r.Remaining, Maximum = r.Maximum })],
                HitDiePools = [.. c.Play.HitDiePools.Select(p => new HitDiePool { Id = p.Id, Remaining = p.Remaining, Maximum = p.Maximum })],
                SpellNotes = c.Play.SpellNotes, DreadUsedThisTurn = c.Play.DreadUsedThisTurn, HunterTarget = c.Play.HunterTarget,
                LastRoll = c.Play.LastRoll, LastCast = c.Play.LastCast, CompletedOperationIds = c.Play.CompletedOperationIds, UndoHistory = c.Play.UndoHistory
            }
        };
    }

    /// <summary>The stored facts as the "existing" character the rules recalculate around.</summary>
    public static CharacterState Seed(CharacterRecord r)
    {
        var f = r.Facts;
        return new CharacterState
        {
            Id = r.Id, Name = r.Build.Name, ClassName = r.Build.ClassName, Subclass = r.Build.Subclass, Species = r.Build.Species, Level = r.Build.Level,
            CurrentHp = f.CurrentHp, TemporaryHp = f.TemporaryHp, Turn = f.Turn, Exhaustion = f.Exhaustion,
            Equipment = f.Equipment, Notes = f.Notes, Effects = f.Effects, Combat = f.Combat, Sheet = f.Sheet, KnownTarget = f.KnownTarget, Defenses = f.Defenses,
            Resources = f.Resources,
            Play = new PlayState
            {
                SpellNotes = f.SpellNotes, DreadUsedThisTurn = f.DreadUsedThisTurn, HunterTarget = f.HunterTarget, HitDiePools = f.HitDiePools,
                LastRoll = f.LastRoll, LastCast = f.LastCast, CompletedOperationIds = f.CompletedOperationIds, UndoHistory = f.UndoHistory
            }
        };
    }
}

/// <summary>Rebuilds a full character from its record through the rules.</summary>
public sealed class CharacterHydrator(ICharacterRules rules)
{
    public Task<CharacterState> HydrateAsync(CharacterRecord record, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var seed = CharacterRecords.Seed(record);
        // The rules read the inventory for equipped armor and weapons; without it they would fall back to starting gear.
        var draft = new CharacterDraft { Id = record.Id, Build = record.Build, Inventory = [.. record.Facts.Equipment], IncludeDefaultEquipment = false, Facts = record.Facts };
        var projected = rules.Recalculate(draft, seed);
        return Task.FromResult(projected with
        {
            CurrentHp = Math.Min(seed.CurrentHp, projected.MaximumHp), TemporaryHp = seed.TemporaryHp,
            Play = projected.Play with { UndoHistory = seed.Play.UndoHistory, LastRoll = seed.Play.LastRoll, LastCast = seed.Play.LastCast, CompletedOperationIds = seed.Play.CompletedOperationIds }
        });
    }
}

/// <summary>The session's store: records in, recalculated characters out.</summary>
public sealed class RecordWorkspaceStore(ICharacterRecordStore records, CharacterHydrator hydrator) : IWorkspaceStore
{
    public Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default) => records.ListAsync(ct);

    public Task<IReadOnlyList<CatalogItem>> HomebrewItemsAsync(CancellationToken ct = default) => records.HomebrewItemsAsync(CharacterRecord.DefaultOwnerId, ct);

    public async Task<WorkspaceSnapshot?> LoadAsync(string id, CancellationToken ct = default)
        => await records.LoadAsync(id, ct) is { } record ? await HydrateAsync(record, ct) : null;

    public async Task<WorkspaceSnapshot> SaveAsync(WorkspaceSnapshot snapshot, long expectedRevision, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (expectedRevision < 0 || snapshot.Revision != expectedRevision)
            throw new WorkspaceConflictException("This workspace revision is stale. Reload before saving.");
        Validate(snapshot);
        var saved = await records.SaveAsync(CharacterRecords.FromState(snapshot.Character, expectedRevision), expectedRevision, ct);
        return await HydrateAsync(saved, ct);
    }

    public void Validate(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        WorkspaceLimits.Validate(snapshot);
        _ = CharacterRecords.FromState(snapshot.Character, snapshot.Revision);
    }

    private async Task<WorkspaceSnapshot> HydrateAsync(CharacterRecord record, CancellationToken ct)
    {
        try { return new WorkspaceSnapshot { Revision = record.Revision, Character = await hydrator.HydrateAsync(record, ct) }; }
        catch (ArgumentException error) { throw new InvalidDataException("A stored character could not be recalculated by the rules.", error); }
    }
}
