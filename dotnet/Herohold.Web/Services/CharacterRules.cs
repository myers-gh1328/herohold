using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Documents;
using Herohold.Rules;

namespace Herohold.Web.Services;

/// <summary>The character rules as the session uses them: drafts in, settled drafts and characters out.</summary>
public interface ICharacterRules
{
    DraftResult Create();
    /// <summary>The draft as the rules settle it. <paramref name="existing"/> is the saved character a draft edits.</summary>
    DraftResult Calculate(CharacterDraft draft, CharacterState? existing = null);
    /// <summary>The finished character; refused while the draft still has unresolved choices.</summary>
    CharacterState Adopt(CharacterDraft draft, CharacterState? existing = null, bool addGainedHp = false);
    /// <summary>A saved character recalculated from its draft, such as after loading or an equipment change.</summary>
    CharacterState Recalculate(CharacterDraft draft, CharacterState existing);
    DraftResult Edit(CharacterDraft draft, IReadOnlyList<string> path, ChoiceValue value, CharacterState? existing = null);
    /// <summary>Applies several build values at once, given as named parts.</summary>
    DraftResult Edit(CharacterDraft draft, ChoiceValue patch, CharacterState? existing = null);
    DraftResult GenerateAbilityScores(CharacterDraft draft, string method, CharacterState? existing = null);
    DraftResult AssignAbilityScore(CharacterDraft draft, Ability ability, int index, CharacterState? existing = null);
    DraftResult RepairFeat(CharacterDraft draft, string instanceId, ChoiceValue value, CharacterState? existing = null);
    LevelUpResult StartLevelUp(CharacterState source, int targetLevel, string? targetClass = null);
    LevelUpResult ChooseLevelUp(CharacterState source, LevelUpDraft draft, string choiceId, ChoiceValue value);
    LevelUpResult PreviewLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp);
    CharacterState CommitLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp);
    /// <summary>An uploaded rules character document, read once into a draft for review.</summary>
    DraftResult ImportJson(JsonObject document);
    /// <summary>An uploaded PDF reviewed with the given recovered selections (none for a first review).</summary>
    Task<DraftResult> ImportPdfAsync(byte[] pdf, ChoiceValue? selections = null, CancellationToken ct = default);
    /// <summary>An edited import draft checked again against its import review.</summary>
    DraftResult Revalidate(DraftResult edited, ImportReview review);
    /// <summary>Top-level fields of an uploaded document that Herohold neither stores nor recalculates.</summary>
    List<string> DroppedFields(JsonObject uploaded, CharacterDraft reviewed);
    Task<byte[]> ExportAsync(CharacterState character, string format, CancellationToken ct = default);
    /// <summary>Item and weapon names the inventory editor offers.</summary>
    ItemCatalog Items();
}

public sealed class CharacterRules(NativeCharacterBuilder builder, CharacterEditRules edits, NativeLevelUpRules leveling, RulesCatalogSnapshot catalog, IPdfService pdf, NativePdfImportReviewService? importReview = null) : ICharacterRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private const string Incomplete = "Resolve the required character choices before confirming.";

    public DraftResult Create()
    {
        var created = CanonicalCharacterRules.Create();
        var result = Calculate(CharacterDraft.FromDocument(created));
        // A brand-new character starts at full HP.
        return result with { Summary = result.Summary with { CurrentHp = result.Summary.MaximumHp } };
    }

    public DraftResult Calculate(CharacterDraft draft, CharacterState? existing = null) => Evaluate(draft, existing, false).View;

    public CharacterState Adopt(CharacterDraft draft, CharacterState? existing = null, bool addGainedHp = false)
    {
        var evaluation = Evaluate(draft, existing, addGainedHp);
        if (!evaluation.View.Validation.Complete) throw new InvalidOperationException(Incomplete);
        return WorkspaceProjection.FromCalculation(evaluation.Calculation, evaluation.Source);
    }

    public CharacterState Recalculate(CharacterDraft draft, CharacterState existing)
        => WorkspaceProjection.FromCalculation(Evaluate(draft, existing, false).Calculation, existing);

    public DraftResult Edit(CharacterDraft draft, IReadOnlyList<string> path, ChoiceValue value, CharacterState? existing = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0 || path.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Choose a build value to change.");
        var patch = value;
        for (var i = path.Count - 1; i >= 0; i--) patch = ChoiceValue.Record([new(path[i], patch)]);
        return Edit(draft, patch, existing);
    }

    public DraftResult Edit(CharacterDraft draft, ChoiceValue patch, CharacterState? existing = null)
    {
        var fields = patch.Fields ?? throw new CharacterWorkflowException("unsupported_edit", "Choose supported character fields to edit.");
        foreach (var (key, value) in fields.Where(p => CharacterEditRules.DraftFacts.Contains(p.Key))) draft = EditFact(draft, key, value);
        var build = fields.Where(p => !CharacterEditRules.DraftFacts.Contains(p.Key)).ToArray();
        if (build.Length > 0) draft = draft with { Build = edits.Apply(draft.Build, ChoiceValue.Record(build)) };
        return Calculate(draft, existing);
    }

    /// <summary>Sets one of the draft's own facts, replacing it whole: its notes or the equipment it names beside its packages.</summary>
    private static CharacterDraft EditFact(CharacterDraft draft, string key, ChoiceValue value)
    {
        var shape = new ArgumentException(key + " has an unsupported shape.");
        return key switch
        {
            "notes" => draft with { Notes = value.Text ?? throw shape },
            "include_default_equipment" => draft with { IncludeDefaultEquipment = value.Flag ?? throw shape },
            "custom_equipment" => draft with { CustomEquipment = value.Items is { } items ? [.. items.Select(i => i.Text ?? throw shape)] : throw shape },
            _ => draft with
            {
                EquipmentBonuses = (value.Fields ?? throw shape).ToDictionary(item => item.Key, item => (item.Value.Fields ?? throw shape).ToDictionary(
                    p => Enum.TryParse<Ability>(p.Key, out var a) && Enum.IsDefined(a) && a.ToString() == p.Key ? a : throw shape, p => p.Value.Number ?? throw shape), StringComparer.Ordinal)
            }
        };
    }

    public DraftResult GenerateAbilityScores(CharacterDraft draft, string method, CharacterState? existing = null)
    {
        var chosen = method switch
        {
            "Standard array" => AbilityGenerationMethod.StandardArray,
            "Roll 4d6" => AbilityGenerationMethod.Roll4d6,
            _ => throw new CharacterWorkflowException("invalid_ability_method", "Choose Standard array or Roll 4d6.")
        };
        return Calculate(draft with { Build = AbilityGenerationRules.Generate(draft.Build, chosen) }, existing);
    }

    public DraftResult AssignAbilityScore(CharacterDraft draft, Ability ability, int index, CharacterState? existing = null)
    {
        if (!Enum.IsDefined(ability) || index is < 0 or > 5) throw new CharacterWorkflowException("invalid_ability_assignment", "Choose an ability and an available score position.");
        return Calculate(draft with { Build = AbilityGenerationRules.Swap(draft.Build, ability, index) }, existing);
    }

    public DraftResult RepairFeat(CharacterDraft draft, string instanceId, ChoiceValue value, CharacterState? existing = null)
    {
        if (value.Fields is null) throw new CharacterWorkflowException("invalid_feat", "Choose structured feat options.");
        return Calculate(draft with { Build = builder.RepairFeat(draft, instanceId, value) }, existing);
    }

    public LevelUpResult StartLevelUp(CharacterState source, int targetLevel, string? targetClass = null)
        => LevelUp(source, Guard(() => targetClass is { Length: > 0 } target ? leveling.Start(Rules(source), targetLevel, target) : leveling.Start(Rules(source), targetLevel)), new());

    public LevelUpResult ChooseLevelUp(CharacterState source, LevelUpDraft draft, string choiceId, ChoiceValue value)
        => LevelUp(source, Guard(() => leveling.Choose(Rules(source), draft, choiceId, value)), new());

    public LevelUpResult PreviewLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp)
        => LevelUp(source, Guard(() => leveling.Preview(Rules(source), draft, Options(hp))), hp);

    public CharacterState CommitLevelUp(CharacterState source, LevelUpDraft draft, LevelUpHp hp)
    {
        var review = Guard(() => leveling.Commit(Rules(source), draft, Options(hp)));
        return Adopt(CharacterDraft.Of(source) with { Build = review.Build }, source, hp.AddGainedHp);
    }

    private LevelUpResult LevelUp(CharacterState source, LevelUpReview review, LevelUpHp hp)
    {
        var before = Evaluate(CharacterDraft.Of(source), source, hp.AddGainedHp).View;
        var leveled = Evaluate(CharacterDraft.Of(source) with { Build = review.Build }, source, hp.AddGainedHp).View;
        return new()
        {
            Review = review, Validation = before.Validation, Candidate = leveled.Summary,
            PendingChoices = [.. review.PendingChoices.Select(BuilderChoices.From)],
            PreviewValidation = new()
            {
                Complete = review.CanFinalize,
                Missing = [.. review.Issues.Where(i => i.Severity == BuildIssueSeverity.Error).Select(i => i.Message).Distinct()],
                Warnings = [.. review.Issues.Where(i => i.Severity != BuildIssueSeverity.Error).Select(i => i.Message).Distinct()]
            }
        };
    }

    private static DraftCharacter Rules(CharacterState source) => new(CharacterDraft.Of(source).Build, source.Equipment);
    private static LevelUpHpOptions Options(LevelUpHp hp) => new()
    {
        Method = hp.Mode switch
        {
            "" or "fixed" => HitPointMethod.Fixed,
            "rolled" => HitPointMethod.Rolled,
            _ => throw new CharacterWorkflowException("invalid_hp", "Choose fixed HP or enter the unmodified Hit Die roll.")
        },
        Roll = hp.Roll, AddGainedHp = hp.AddGainedHp
    };
    private static LevelUpReview Guard(Func<LevelUpReview> step)
    {
        try { return step(); }
        catch (LevelUpException error) { throw new CharacterWorkflowException(error.Code, error.Message); }
    }

    public DraftResult ImportJson(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var view = Calculate(builder.Import(document));
        return view with
        {
            Review = new()
            {
                CanAdopt = view.Validation.Complete, SourceCanAdopt = true, SourceKind = "json",
                Message = "Review this character before adding a separate copy to the library.", DroppedFields = DroppedFields(document, view.Draft)
            }
        };
    }

    public async Task<DraftResult> ImportPdfAsync(byte[] bytes, ChoiceValue? selections = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (importReview is null) throw new CharacterWorkflowException("pdf_review_unavailable", "PDF review is not configured.");
        PdfReview reviewed;
        try { reviewed = await importReview.ReviewAsync(bytes, selections ?? new(), ct); }
        catch (PdfFormatException error) { throw new CharacterWorkflowException(error.Code, error.Message, error); }
        var draft = reviewed.Draft;
        return new()
        {
            Draft = draft, Summary = Calculate(draft).Summary, Validation = reviewed.Validation, Review = reviewed.Review,
            Choices = [.. reviewed.Choices.Where(c => draft.Inventory is null || !BuilderChoices.IsEquipmentPackage(c.Id))]
        };
    }

    public DraftResult Revalidate(DraftResult edited, ImportReview review)
    {
        if (review.Selections is null)
            return edited with { Review = review with { CanAdopt = edited.Validation.Complete, Message = "Review the completed character before adoption." } };
        var issues = review.Issues.Where(i => !i.Code.StartsWith("validation_", StringComparison.Ordinal) && !i.Code.StartsWith("feat_", StringComparison.Ordinal)).ToList();
        issues.AddRange(edited.Validation.Missing.Select(m => Issue("validation_missing", m, "blocker")));
        issues.AddRange(edited.Validation.Warnings.Select(m => Issue("validation_warnings", m, "warning")));
        return edited with { Review = review with { Issues = issues, CanAdopt = review.SourceCanAdopt && edited.Validation.Complete && !issues.Any(i => i.Blocks) } };
        static ImportIssue Issue(string code, string message, string severity) => new()
        {
            Code = code, Severity = severity, Label = message,
            Remedy = severity == "blocker" ? "Review this choice before adopting the character." : "Review whether the recovered choices explain this difference."
        };
    }

    public List<string> DroppedFields(JsonObject uploaded, CharacterDraft reviewed)
    {
        var kept = BuildFacts.StoredKeys.Concat(WorkspaceProjection.Sidecars).Concat(WorkspaceProjection.CalculatedKeys).Concat(["schema_version", "character_id", "notes", "folio_play_metadata", "folio_sheet"]).ToHashSet(StringComparer.Ordinal);
        return [.. uploaded.Select(p => p.Key).Where(key => !kept.Contains(key)).Order(StringComparer.Ordinal)];
    }

    public async Task<byte[]> ExportAsync(CharacterState character, string format, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (format is not ("json" or "pdf")) throw new ArgumentException("Choose a supported export format.");
        var document = WorkspaceProjection.ExportDocument(character);
        try { return format == "json" ? JsonSerializer.SerializeToUtf8Bytes(document, Json) : await pdf.ExportAsync(character, document, ct); }
        catch (PdfFormatException error) { throw new CharacterWorkflowException(error.Code, error.Message, error); }
    }

    public ItemCatalog Items()
    {
        static List<string> Names(IEnumerable<string> names) => [.. names.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
        return new(Names(catalog.Equipment), Names(catalog.WeaponRecords.Select(w => w.Name)));
    }

    /// <summary>The calculation of a draft around the character it edits, or the play facts it brings.</summary>
    private (DraftResult View, CharacterCalculation Calculation, CharacterState? Source) Evaluate(CharacterDraft draft, CharacterState? existing, bool addGainedHp)
    {
        var source = existing ?? Seed(draft);
        var calculation = builder.Calculate(draft, source, addGainedHp);
        return (View(draft, calculation), calculation, source);
    }

    /// <summary>An imported draft's play facts as the character the rules recalculate around.</summary>
    private CharacterState? Seed(CharacterDraft draft)
    {
        if (draft.Facts is not { } facts) return null;
        var equipment = draft.Inventory ?? [.. builder.StartingGear(draft)];
        return CharacterRecords.Seed(new() { Id = draft.Id, Build = draft.Build, Facts = facts with { Equipment = equipment } });
    }

    private static DraftResult View(CharacterDraft draft, CharacterCalculation c) => new()
    {
        Draft = draft with { Build = c.Build, CustomEquipment = c.CustomEquipment }, Validation = c.Validation,
        Summary = new()
        {
            Name = c.Build.Name, Species = c.Build.Species, ClassName = c.Build.ClassName, Subclass = c.Build.Subclass, Level = c.Build.Level, ClassLevels = c.Build.ClassLevels,
            MaximumHp = c.MaximumHp, CurrentHp = c.CurrentHp, AbilityMethod = c.Build.AbilityScoreMethod is { Length: > 0 } method ? method : "Manual entry", RolledScores = c.Build.RolledAbilityScores ?? [],
            Assignments = Enum.GetValues<Ability>().Where(a => c.Build.AbilityScoreAssignments?.ContainsKey(a.ToString()) == true).ToDictionary(a => a, a => c.Build.AbilityScoreAssignments![a.ToString()])
        },
        // A draft that already carries its own inventory never asks for a starting equipment package.
        Choices = [.. c.Choices.Select(BuilderChoices.From).Where(choice => draft.Inventory is null || !BuilderChoices.IsEquipmentPackage(choice.Id))]
    };

}

/// <summary>The rules' builder choices as the builder shows them.</summary>
public static class BuilderChoices
{
    public static bool IsEquipmentPackage(string id) => id is "class_options_equipment" or "background_options_equipment";

    public static BuilderChoice From(BuilderChoiceDefinition d)
    {
        var options = d.Options.Select(Option).ToList();
        return new()
        {
            Id = d.Id, Label = d.Label, Section = d.Section is { Length: > 0 } section ? section : "choices",
            Type = d.Details.UiType ?? d.Mode switch { BuildChoiceMode.Text => "text", BuildChoiceMode.Multi => "multi", BuildChoiceMode.AbilityScores => "ability_scores", BuildChoiceMode.AsiOrFeat => "asi_or_feat", _ => "single" },
            Required = d.Required, Choose = d.Choose, Status = d.Status.ToString().ToLowerInvariant(), Path = [.. d.Path], Options = options,
            Value = (d.Details.EffectiveSelection ?? d.PreservedSelection) is { IsNull: false } value ? value : d.Mode == BuildChoiceMode.Multi ? ChoiceValue.ListOf([]) : ChoiceValue.Of(""),
            Minimum = d.Details.Minimum, Maximum = d.Details.Maximum,
            FeatOptions = d.Details.FeatOptions.Count > 0 ? [.. d.Details.FeatOptions.Select(Option)] : options
        };
    }

    private static BuilderOption Option(BuildChoiceOption o) => new()
    {
        Value = o.Value, Label = o.Label, Description = o.Description, Source = o.Source, Reason = o.Reason, Disabled = o.Disabled,
        Children = [.. o.Children.Select(From)]
    };
}
