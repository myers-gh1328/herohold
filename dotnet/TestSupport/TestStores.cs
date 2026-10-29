using Herohold.Core;
using Herohold.Storage;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Herohold.TestSupport;

/// <summary>A SQLite character store in a test folder, recalculated by the real rules (built once per test run).</summary>
public static class TestStores
{
    private static readonly Lazy<ServiceProvider> Rules = new(() => new ServiceCollection()
        .AddHeroholdRules(Root(), Path.Combine(AppContext.BaseDirectory, "pdf-worker", "Herohold.PdfWorker.dll")).BuildServiceProvider());

    public static ICharacterRules Workflow => Rules.Value.GetRequiredService<ICharacterRules>();
    public static NativeCharacterBuilder Builder => Rules.Value.GetRequiredService<NativeCharacterBuilder>();
    public static CharacterHydrator Hydrator => new(Workflow);

    /// <summary>The character a rules character document calculates to, as a fixture; it need not be complete.</summary>
    public static CharacterState Calculated(System.Text.Json.Nodes.JsonObject raw, CharacterState? existing = null)
        => WorkspaceProjection.FromCalculation(Builder.Calculate(Builder.Import(raw), existing, false), existing);

    /// <summary>
    /// The canonical character document an old JSON save kept for <paramref name="c"/>: the rules' own copy,
    /// with the sheet and play metadata they returned.
    /// </summary>
    /// <summary>What the rules calculate for a saved character, for tests that change part of it.</summary>
    internal static CharacterCalculation Calculation(CharacterState c) => Builder.Calculate(CharacterDraft.Of(c), c, false);

    /// <summary>The character a rules result (from <see cref="NativeCharacterBuilder.ResultFor"/>) describes; it need not be complete.</summary>
    public static CharacterState Character(System.Text.Json.Nodes.JsonObject result) => Calculated(result["character"]!.AsObject());

    /// <summary>A character document read back as the character it describes, with the play facts in its Herohold sidecars.</summary>
    public static CharacterState Restored(System.Text.Json.Nodes.JsonObject raw)
    {
        var draft = Builder.Import(raw);
        var seed = CharacterRecords.Seed(new() { Id = draft.Id, Build = draft.Build, Facts = draft.Facts! with { Equipment = draft.Inventory ?? [.. Builder.StartingGear(draft)] } });
        return WorkspaceProjection.FromCalculation(Builder.Calculate(draft, seed, false), seed);
    }

    public static Task<System.Text.Json.Nodes.JsonObject> OldSaveCanonicalAsync(CharacterState c)
    {
        var calculation = Builder.Calculate(CharacterDraft.Of(c), c, false);
        var raw = BuilderDocuments.Document(calculation);
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        raw["folio_play_metadata"] = new System.Text.Json.Nodes.JsonObject
        {
            ["attacks"] = System.Text.Json.JsonSerializer.SerializeToNode(calculation.Attacks, json), ["grants"] = System.Text.Json.JsonSerializer.SerializeToNode(calculation.Grants, json),
            ["grantSources"] = System.Text.Json.JsonSerializer.SerializeToNode(calculation.GrantSources, json), ["attackActionCount"] = calculation.AttackActionCount
        };
        raw["folio_sheet"] = new System.Text.Json.Nodes.JsonObject
        {
            ["feature_details"] = new System.Text.Json.Nodes.JsonArray([.. calculation.FeatureDetails.Select(d => (System.Text.Json.Nodes.JsonNode?)new System.Text.Json.Nodes.JsonObject { ["name"] = d.Name, ["description"] = d.Description, ["source"] = d.Source, ["related_features"] = new System.Text.Json.Nodes.JsonArray([.. d.RelatedFeatures.Select(r => (System.Text.Json.Nodes.JsonNode?)r.Name)]) })]),
            ["spells"] = new System.Text.Json.Nodes.JsonObject { ["spellbook"] = new System.Text.Json.Nodes.JsonArray([.. calculation.Spellbook.Select(n => (System.Text.Json.Nodes.JsonNode?)n)]) }
        };
        foreach (var key in WorkspaceProjection.Sidecars) raw.Remove(key);
        return Task.FromResult(raw);
    }

    public static IWorkspaceStore Open(string folder) => new RecordWorkspaceStore(Records(folder), Hydrator);
    public static SqliteCharacterRecordStore Records(string folder) => new(Path.Combine(folder, "herohold.db"));

    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "data", "imported", "5etools", "classes.json"))) return current.FullName;
        throw new InvalidOperationException("The local rules data is required for these tests.");
    }
}

/// <summary>
/// Keeps hand-made characters exactly as given, for UI tests of states the rules would not produce.
/// Storage itself is covered by tests that use real characters through <see cref="TestStores.Open"/>.
/// </summary>
public sealed class HandMadeStore : IWorkspaceStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HandMadeStore> Folders = new();
    private readonly Dictionary<string, WorkspaceSnapshot> saved = [];

    /// <summary>One store per folder, so a new session over the same folder sees earlier saves.</summary>
    public static HandMadeStore At(string folder) => Folders.GetOrAdd(Path.GetFullPath(folder), _ => new());

    public Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default)
    { lock (saved) return Task.FromResult<IReadOnlyList<WorkspaceSummary>>([.. saved.Values.OrderBy(s => s.Character.Id, StringComparer.Ordinal).Select(s => new WorkspaceSummary(s.Character.Id, s.Character.Name, s.Character.Species, s.Character.ClassName, s.Character.Level, s.Revision))]); }

    public Task<WorkspaceSnapshot?> LoadAsync(string id, CancellationToken ct = default)
    { lock (saved) return Task.FromResult(saved.TryGetValue(id, out var s) ? Copy(s) : null); }

    public Task<WorkspaceSnapshot> SaveAsync(WorkspaceSnapshot snapshot, long expectedRevision, CancellationToken ct = default)
    {
        if (expectedRevision < 0 || snapshot.Revision != expectedRevision) throw new WorkspaceConflictException("This workspace revision is stale. Reload before saving.");
        Validate(snapshot);
        lock (saved)
        {
            if ((saved.GetValueOrDefault(snapshot.Character.Id)?.Revision ?? 0) != expectedRevision) throw new WorkspaceConflictException("A newer workspace revision exists. Reload before saving.");
            var next = Copy(snapshot with { SchemaVersion = 3, Revision = expectedRevision + 1 });
            saved[next.Character.Id] = next;
            return Task.FromResult(Copy(next));
        }
    }

    public void Validate(WorkspaceSnapshot snapshot) => WorkspaceLimits.Validate(snapshot);

    private static WorkspaceSnapshot Copy(WorkspaceSnapshot s) => s with { Character = DomainCopy.Clone(s.Character) };
}
