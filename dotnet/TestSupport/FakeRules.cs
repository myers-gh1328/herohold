using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;

namespace Herohold.TestSupport;

/// <summary>
/// Rules that answer each step with a prepared result, for session and UI tests of states the real rules would not
/// produce. A result names a calculated character, its validation, choices, any import review and any level-up draft,
/// in the rules' document shape. Build edits are applied to that character; nothing is recalculated.
/// </summary>
public class FakeRules(Func<string, JsonObject> answer) : ICharacterRules
{
    public FakeRules(JsonObject result) : this(_ => result.DeepClone().AsObject()) { }

    /// <summary>Called with the step's name before it answers; a test can hold or fail a step here.</summary>
    protected virtual void Step(string operation) { }

    private JsonObject Answer(string operation) { Step(operation); return answer(operation); }

    public virtual DraftResult Create() => View(Answer("create"), null);
    public virtual DraftResult Calculate(CharacterDraft draft, CharacterState? existing = null) => View(Answer("calculate"), draft);
    public virtual CharacterState Adopt(CharacterDraft draft, CharacterState? existing = null, bool addGainedHp = false)
    {
        var result = Answer("adopt");
        if (result["validation"]?["complete"]?.GetValue<bool>() != true) throw new InvalidOperationException("Resolve the required character choices before confirming.");
        return TestStores.Calculated(Character(result, draft), existing);
    }
    public virtual CharacterState Recalculate(CharacterDraft draft, CharacterState existing) => existing;
    public virtual DraftResult Edit(CharacterDraft draft, IReadOnlyList<string> path, ChoiceValue value, CharacterState? existing = null)
    {
        var patch = value;
        for (var i = path.Count - 1; i >= 0; i--) patch = ChoiceValue.Record([new(path[i], patch)]);
        return Edit(draft, patch, existing);
    }
    public virtual DraftResult Edit(CharacterDraft draft, ChoiceValue patch, CharacterState? existing = null)
    {
        var result = Answer("edit");
        var character = Character(result, draft);
        foreach (var (key, value) in patch.Fields ?? []) character[key] = JsonSerializer.SerializeToNode(value);
        return View(result, draft with { Build = BuildFacts.FromCanonical(character) });
    }
    public virtual DraftResult GenerateAbilityScores(CharacterDraft draft, string method, CharacterState? existing = null) => View(Answer("ability.generate"), draft);
    public virtual DraftResult AssignAbilityScore(CharacterDraft draft, Ability ability, int index, CharacterState? existing = null) => View(Answer("ability.assign"), draft);
    public virtual DraftResult RepairFeat(CharacterDraft draft, string instanceId, ChoiceValue value, CharacterState? existing = null) => View(Answer("feat.choose"), draft);
    public virtual LevelUpResult StartLevelUp(CharacterState source, int targetLevel, string? targetClass = null) => LevelUp(Answer("levelup.start"));
    public virtual LevelUpResult ChooseLevelUp(CharacterState source, LevelUpDraft draft, string choiceId, ChoiceValue value) => LevelUp(Answer("levelup.choose"));
    public virtual LevelUpResult PreviewLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp) => LevelUp(Answer("levelup.preview"));
    public virtual CharacterState CommitLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp)
        => TestStores.Calculated(Answer("levelup.commit")["character"]!.DeepClone().AsObject(), source);
    public virtual DraftResult ImportJson(JsonObject document) => View(Answer("import.json"), null);
    public virtual Task<DraftResult> ImportPdfAsync(byte[] pdf, ChoiceValue? selections = null, CancellationToken ct = default)
    {
        var result = Answer("import.pdf");
        if (result["importReview"] is JsonObject review && review["selections"] is null) review["selections"] = JsonSerializer.SerializeToNode(selections ?? ChoiceValue.Record([]));
        return Task.FromResult(View(result, null));
    }
    public virtual DraftResult Revalidate(DraftResult edited, ImportReview review) => edited with { Review = review };
    public virtual List<string> DroppedFields(JsonObject uploaded, CharacterDraft reviewed) => [];
    public virtual Task<byte[]> ExportAsync(CharacterState character, string format, CancellationToken ct = default)
        => Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(WorkspaceProjection.ToCanonical(character)));

    public virtual ItemCatalog Items()
    {
        var catalogs = Answer("catalog")["catalogs"];
        List<string> Names(string key) => [.. (catalogs?[key] as JsonArray ?? []).Select(n => n?.GetValue<string>() ?? "").Where(n => n.Length > 0)];
        return new(Names("equipment"), Names("weapons"));
    }

    /// <summary>The prepared character with the draft's build, when a draft is given.</summary>
    private static JsonObject Character(JsonObject result, CharacterDraft? draft)
    {
        var character = result["character"]?.DeepClone().AsObject() ?? WorkspaceProjection.ToCanonical(SampleWorkspace.Create().Character);
        return draft is null ? character : draft.Build.ReplaceIn(character, draft.Id);
    }

    private static DraftResult View(JsonObject result, CharacterDraft? draft)
    {
        var character = Character(result, draft);
        return new()
        {
            Draft = (draft ?? CharacterDraft.FromDocument(character)) with { Build = BuildFacts.FromCanonical(character) },
            Summary = Summary(character),
            Choices = [.. (result["choices"] as JsonArray ?? []).OfType<JsonObject>().Select(JsonWorkflowViews.Choice)],
            Validation = JsonWorkflowViews.Validation(result["validation"]) ?? new(),
            Review = result["importReview"] is JsonObject review ? JsonWorkflowViews.ReadImportReview(review) : null
        };
    }

    private static LevelUpResult LevelUp(JsonObject result)
    {
        var draft = result["draft"] as JsonObject ?? new();
        int Number(string key) => draft[key] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
        return new()
        {
            Review = new()
            {
                Draft = new() { TransactionId = draft["transaction_id"]?.GetValue<string>() ?? "", ClassName = draft["class_name"]?.GetValue<string>() ?? "", CurrentLevel = Number("current_level"), TargetLevel = Number("target_level") },
                HitDie = Number("hit_die"), HpBaseGain = Number("hp_base_gain"), HpIncrease = Number("hp_increase"),
                AutomaticFeatures = [.. (draft["automatic_features"] as JsonArray ?? []).Select(f => f?.GetValue<string>() ?? "")]
            },
            Validation = JsonWorkflowViews.Validation(result["validation"]) ?? new(),
            Candidate = Summary(result["character"] as JsonObject ?? new()),
            PendingChoices = [.. (result["choices"] as JsonArray ?? []).OfType<JsonObject>().Select(JsonWorkflowViews.Choice)],
            PreviewValidation = JsonWorkflowViews.Validation(result["previewValidation"]) ?? new() { Complete = true }
        };
    }

    private static DraftSummary Summary(JsonObject c)
    {
        int? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
        string Text(string key) => c[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        return new()
        {
            Name = Text("name"), Species = Text("species"), ClassName = Text("class"), Subclass = Text("subclass"), Level = Number(c["level"]),
            MaximumHp = Number(c["hit_points"]?["maximum"] ?? c["hit_points"]?["max"]), CurrentHp = Number(c["hit_points"]?["current"]),
            AbilityMethod = Text("ability_score_method") is { Length: > 0 } method ? method : "Manual entry",
            RolledScores = c["rolled_ability_scores"] is JsonArray rolled ? [.. rolled.Select(n => Number(n) ?? 0)] : [],
            Assignments = Enum.GetValues<Ability>().Where(a => Number(c["ability_score_assignments"]?[a.ToString()]) is not null).ToDictionary(a => a, a => Number(c["ability_score_assignments"]?[a.ToString()])!.Value)
        };
    }
}
