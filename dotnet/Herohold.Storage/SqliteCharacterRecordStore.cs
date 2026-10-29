using Herohold.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Herohold.Storage;

/// <summary>Character records in one SQLite database. A save replaces the character's rows in one transaction.</summary>
public sealed class SqliteCharacterRecordStore : ICharacterRecordStore
{
    public const int MaximumCharacters = 1000;
    private readonly DbContextOptions<HeroholdDbContext> options;

    public SqliteCharacterRecordStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var full = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var connection = new SqliteConnectionStringBuilder { DataSource = full, Pooling = false, ForeignKeys = true }.ToString();
        options = new DbContextOptionsBuilder<HeroholdDbContext>().UseSqlite(connection).Options;
        using var db = new HeroholdDbContext(options);
        db.Database.Migrate();
        if (!db.Users.Any(u => u.Id == CharacterRecord.DefaultOwnerId))
        {
            db.Users.Add(new UserRow { Id = CharacterRecord.DefaultOwnerId, DisplayName = "Owner", CreatedAt = DateTimeOffset.UtcNow });
            db.SaveChanges();
        }
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var db = new HeroholdDbContext(options);
        return (await db.Characters.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new WorkspaceSummary(c.Id, c.Name, c.Species, c.ClassName, c.Level, c.Revision)).ToListAsync(ct)).AsReadOnly();
    }

    public async Task<CharacterRecord?> LoadAsync(string id, CancellationToken ct = default)
    {
        CharacterRecord.ValidateId(id);
        await using var db = new HeroholdDbContext(options);
        return (await ReadAsync(db, id, ct))?.ToRecord();
    }

    public async Task<CharacterRecord> SaveAsync(CharacterRecord record, long expectedRevision, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CharacterRecord.ValidateId(record.Id);
        if (expectedRevision < 0 || record.Revision != expectedRevision)
            throw new WorkspaceConflictException("This workspace revision is stale. Reload before saving.");
        if (expectedRevision == long.MaxValue) throw new WorkspaceConflictException("This workspace revision cannot advance safely.");
        var next = record with { Revision = expectedRevision + 1 };
        var rows = CharacterRows.From(next, DateTimeOffset.UtcNow);
        await using var db = new HeroholdDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Characters.Where(c => c.Id == record.Id).Select(c => (long?)c.Revision).SingleOrDefaultAsync(ct);
        if ((current ?? 0) != expectedRevision)
            throw new WorkspaceConflictException("A newer workspace revision exists. Reload before saving.");
        if (current is null && await db.Characters.CountAsync(ct) >= MaximumCharacters)
            throw new InvalidDataException("The character library is full (1,000 characters).");
        if (current is not null)
        {
            // Child rows cascade with the character row.
            await db.Characters.Where(c => c.Id == record.Id).ExecuteDeleteAsync(ct);
        }
        db.Characters.Add(rows.Character);
        db.AddRange(rows.Lists); db.AddRange(rows.Choices); db.AddRange(rows.Feats); db.AddRange(rows.AbilityAdjustments);
        db.AddRange(rows.FeatureConfigurations); db.AddRange(rows.HpGains); db.AddRange(rows.HpRolls); db.AddRange(rows.LevelUps);
        db.AddRange(rows.ResolvedChoices); db.AddRange(rows.ClassTracks); db.AddRange(rows.Items); db.AddRange(rows.Defenses);
        db.AddRange(rows.Effects); db.AddRange(rows.JournalNotes); db.AddRange(rows.MovementModes); db.AddRange(rows.Companions);
        db.AddRange(rows.CompanionActions); db.AddRange(rows.Resources); db.AddRange(rows.HitDiePools); db.AddRange(rows.SpellSources);
        db.AddRange(rows.Combats); db.AddRange(rows.CombatTimers); db.AddRange(rows.CombatGrants); db.AddRange(rows.Rolls);
        db.AddRange(rows.DamageTerms); db.AddRange(rows.Casts); db.AddRange(rows.PlaySnapshots); db.AddRange(rows.UndoEntries);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return next;
    }

    private static async Task<CharacterRows?> ReadAsync(HeroholdDbContext db, string id, CancellationToken ct)
    {
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (character is null) return null;
        var rows = new CharacterRows { Character = character };
        async Task Fill<T>(IQueryable<T> source, List<T> target) where T : class
            => target.AddRange(await source.AsNoTracking().Where(r => EF.Property<string>(r, "CharacterId") == id).ToListAsync(ct));
        await Fill(db.ListValues, rows.Lists); await Fill(db.Choices, rows.Choices); await Fill(db.Feats, rows.Feats);
        await Fill(db.AbilityAdjustments, rows.AbilityAdjustments); await Fill(db.FeatureConfigurations, rows.FeatureConfigurations);
        await Fill(db.HpGains, rows.HpGains); await Fill(db.HpRolls, rows.HpRolls); await Fill(db.LevelUps, rows.LevelUps);
        await Fill(db.ResolvedChoices, rows.ResolvedChoices); await Fill(db.ClassTracks, rows.ClassTracks); await Fill(db.Items, rows.Items);
        await Fill(db.Defenses, rows.Defenses); await Fill(db.Effects, rows.Effects); await Fill(db.JournalNotes, rows.JournalNotes);
        await Fill(db.MovementModes, rows.MovementModes); await Fill(db.Companions, rows.Companions); await Fill(db.CompanionActions, rows.CompanionActions);
        await Fill(db.Resources, rows.Resources); await Fill(db.HitDiePools, rows.HitDiePools); await Fill(db.SpellSources, rows.SpellSources);
        await Fill(db.Combats, rows.Combats); await Fill(db.CombatTimers, rows.CombatTimers); await Fill(db.CombatGrants, rows.CombatGrants);
        await Fill(db.Rolls, rows.Rolls); await Fill(db.DamageTerms, rows.DamageTerms); await Fill(db.Casts, rows.Casts);
        await Fill(db.PlaySnapshots, rows.PlaySnapshots); await Fill(db.UndoEntries, rows.UndoEntries);
        return rows;
    }
}
