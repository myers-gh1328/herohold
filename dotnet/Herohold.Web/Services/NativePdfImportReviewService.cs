using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Herohold.Core;
using Herohold.Documents;
using Herohold.Rules;
namespace Herohold.Web.Services;

/// <summary>An uploaded PDF reviewed: the character it recovers, the choices still open, and how it compares with the sheet.</summary>
public sealed record PdfReview
{
    public CharacterDraft Draft { get; init; } = new();
    public List<BuilderChoice> Choices { get; init; } = [];
    public WorkflowValidation Validation { get; init; } = new();
    public ImportReview Review { get; init; } = new();
}

/// <summary>
/// Reconciles explicitly uploaded PDFs as detached, source-validated character drafts. A PDF with Herohold's embedded
/// state imports that state; any other compatible sheet is rebuilt from the values recovered from its fields, which
/// the player confirms and corrects through the review's selections.
/// </summary>
public sealed class NativePdfImportReviewService(NativeCharacterBuilder characters, RulesCatalogSnapshot catalog, IPdfService pdf)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Ability[] Abilities = Enum.GetValues<Ability>();
    private const string ReviewRemedy = "Review this choice before adopting the character.";
    private const string DifferenceRemedy = "Review whether the recovered choices explain this difference.";

    // The selections a review recovered, and the player's corrections: named parts, lists of names, numbers and flags.
    private static ChoiceValue? At(ChoiceValue s, params string[] path) => s.At(path) is { IsNull: false } value ? value : null;
    private static string Text(ChoiceValue? v) => v?.Text ?? "";
    private static int? Integer(ChoiceValue? v) => v?.Number ?? (int.TryParse(v?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null);
    private static bool Flag(ChoiceValue? v) => v?.Flag == true;
    private static List<string> Strings(ChoiceValue? v) => v?.Items is { } items ? [.. items.Select(i => i.Text ?? "").Where(s => s.Length > 0)] : [];
    private static List<string>? Optional(ChoiceValue? v) => v is null ? null : Strings(v);
    private static Dictionary<string, ChoiceValue> Fields(ChoiceValue? v) => new(v?.Fields ?? [], StringComparer.Ordinal);
    private static ChoiceValue Names(IEnumerable<string> values) => ChoiceValue.ListOf(values.Distinct(StringComparer.Ordinal).Select(ChoiceValue.Of));
    private static ChoiceValue Record(params (string Key, ChoiceValue Value)[] fields) => ChoiceValue.Record(fields.Select(f => KeyValuePair.Create(f.Key, f.Value)));
    // The PDF's evidence, as the PDF worker reads it from the sheet's fields.
    private static string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";
    private static int? Integer(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var number) ? number : int.TryParse(Text(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    private static IEnumerable<string> Strings(JsonNode? n) => n is JsonArray a ? a.Select(Text).Where(s => s.Length > 0) : [];
    private static string Slug(string value) => new(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray());
    private static ImportIssue Issue(string code, string label, string severity = "blocker", string? observed = null, string? expected = null) => new()
    { Code = code, Severity = severity, Label = label, Observed = observed, Expected = expected, Remedy = severity == "blocker" ? ReviewRemedy : DifferenceRemedy };
    private static bool Blocks(ImportIssue issue) => issue.Severity is "blocker" or "error";

    public async Task<PdfReview> ReviewAsync(byte[] bytes, ChoiceValue selections, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes); ArgumentNullException.ThrowIfNull(selections);
        if (bytes.Length > PdfCodec.MaximumPdfBytes) throw new PdfFormatException("pdf_too_large", "Choose a PDF within the 20 MB import limit.");
        if (JsonSerializer.SerializeToUtf8Bytes(selections).Length > 8 * 1024 * 1024) throw new PdfFormatException("invalid_review", "The PDF review selections exceed supported bounds.");
        var inspection = await pdf.InspectAsync(bytes, cancellationToken);
        if (inspection.Status == "error" || inspection.SourceKind is not ("embedded_state" or "compatible_form") || inspection.TemplateMatch is "unsupported" or "flattened" && inspection.EmbeddedCanonical is null)
            throw new PdfFormatException(inspection.Issues.FirstOrDefault()?.Code ?? "pdf_unsupported", inspection.Issues.FirstOrDefault()?.Label ?? "Choose a supported editable character sheet.");
        var embedded = inspection.SourceKind == "embedded_state";
        var selected = embedded ? ChoiceValue.Record([]) : selections.Fields is { Count: > 0 } ? selections : Initial(inspection);
        ValidateSelections(selected);
        var issues = inspection.Issues.Select(i => new ImportIssue
        {
            Code = i.Code, Severity = i.Severity, Label = i.Label, Remedy = i.Remedy,
            Observed = WorkflowViews.HasValue(i.Observed) ? WorkflowViews.Readable(i.Observed) : null, Expected = WorkflowViews.HasValue(i.Expected) ? WorkflowViews.Readable(i.Expected) : null
        }).ToList();
        var comparisons = new List<(string Key, string Label, int? Observed, int? Calculated)>();
        CharacterDraft draft;
        if (embedded) draft = characters.Import(inspection.EmbeddedCanonical ?? throw new PdfFormatException("embedded_candidate_missing", "The embedded character state is unavailable."));
        else draft = Recover(selected, inspection, issues, cancellationToken);
        var calculation = characters.Calculate(draft, null, false); draft = draft with { Build = calculation.Build };
        int? observedCurrent = null, observedTemporary = null;
        if (embedded) { observedCurrent = calculation.CurrentHp; observedTemporary = calculation.TemporaryHp; }
        else
        {
            var maximum = Integer(At(selected, "observed_maximum_hp"));
            if (maximum is null) issues.Add(Issue("maximum_hp_missing", "Maximum HP needs confirmation."));
            else if (maximum is < 1 or > 100000) issues.Add(Issue("maximum_hp_invalid", "Maximum HP must be between 1 and 100000."));
            else if (Flag(At(selected, "preserve_observed_maximum_hp")))
            {
                draft = draft with { Build = draft.Build with { ImportOverrides = new() { MaximumHpAdjustment = maximum.Value - calculation.MaximumHp } } };
                calculation = characters.Calculate(draft, null, false); draft = draft with { Build = calculation.Build };
            }
            var current = calculation.CurrentHp; var temporary = calculation.TemporaryHp;
            foreach (var field in new[] { "current", "temporary" })
            {
                var observed = Integer(At(selected, "observed_" + field + "_hp"));
                if (observed is < 0 or > 100000) issues.Add(Issue("observed_" + field + "_hp_invalid", "PDF " + field + " HP must be between 0 and 100000."));
                else if (observed.HasValue && field == "current") current = Math.Min(observed.Value, calculation.MaximumHp);
                else if (observed.HasValue) temporary = observed.Value;
            }
            draft = draft with { Facts = (draft.Facts ?? new()) with { CurrentHp = Math.Clamp(current, 0, calculation.MaximumHp), TemporaryHp = temporary } };
            observedCurrent = Integer(At(selected, "observed_current_hp")); observedTemporary = Integer(At(selected, "observed_temporary_hp")) ?? 0;
            foreach (var flag in new[] { ("ability_scores_confirmed", "ability_baseline_unconfirmed", "Confirm the final ability scores and reconstructed baseline."), ("spells_confirmed", "spells_unconfirmed", "Confirm each recovered spell and its rules source."), ("feats_confirmed", "feats_unconfirmed", "Confirm the recovered feats and their choices."), ("masteries_confirmed", "masteries_unconfirmed", "Confirm the recovered Weapon Mastery choices."), ("equipment_confirmed", "equipment_unconfirmed", "Confirm recovered and unmatched equipment.") })
                if (!Flag(At(selected, flag.Item1))) issues.Add(Issue(flag.Item2, flag.Item3));
            var unmatched = Strings(At(selected, "recovered", "unmatched_spells"));
            if (unmatched.Count > 0) issues.Add(Issue("unmatched_spells", "Some PDF spell names do not match the active catalog.", observed: string.Join(", ", unmatched)));
            comparisons = Comparisons(calculation, inspection.ExtractedData);
            foreach (var comparison in comparisons.Where(c => c.Observed.HasValue && c.Observed != c.Calculated))
                issues.Add(Issue("comparison_" + comparison.Key, comparison.Label + " differs from the PDF.", "warning", Readable(comparison.Observed), comparison.Calculated?.ToString(CultureInfo.InvariantCulture)));
        }
        var choices = embedded ? [.. calculation.Choices.Select(BuilderChoices.From)] : Choices(calculation, selected);
        var sourceCanAdopt = !issues.Any(Blocks);
        issues.AddRange(calculation.Validation.Missing.Select(m => Issue("validation_missing", m)));
        issues.AddRange(calculation.Validation.Warnings.Select(m => Issue("validation_warnings", m, "warning")));
        var canAdopt = calculation.Validation.Complete && !issues.Any(Blocks);
        var provenance = (draft.Build.ImportProvenance ?? new()) with
        {
            SourceKind = inspection.SourceKind, Status = canAdopt ? "reviewed" : "review_required", AbilityHistoryReconstructed = !embedded, HpHistoryReconstructed = !embedded,
            Warnings = [.. issues.Where(i => i.Severity == "warning").Select(i => i.Label).Distinct(StringComparer.Ordinal)]
        };
        draft = draft with { Build = draft.Build with { ImportProvenance = provenance } };
        if (observedCurrent is >= 0 and <= 100000 && observedTemporary is >= 0 and <= 100000)
            draft = draft with { Facts = (draft.Facts ?? new()) with { CurrentHp = observedCurrent.Value, TemporaryHp = observedTemporary.Value } };
        cancellationToken.ThrowIfCancellationRequested();
        return new()
        {
            Draft = draft, Choices = choices, Validation = calculation.Validation,
            Review = new()
            {
                CanAdopt = canAdopt, SourceCanAdopt = sourceCanAdopt, SourceKind = inspection.SourceKind, Selections = selected,
                Message = "Review recovered values and warnings before adopting this detached character.", Issues = issues,
                Comparisons = [.. comparisons.Where(c => c.Observed.HasValue).Select(c => new ImportComparisonView { Label = c.Label, Observed = Readable(c.Observed), Calculated = Readable(c.Calculated), Status = c.Observed == c.Calculated ? "match" : "conflict" })],
                Fields = [.. inspection.Fields.Select(p => KeyValuePair.Create(p.Key, WorkflowViews.Readable(p.Value)))]
            }
        };
    }
    private static string Readable(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "—";

    /// <summary>The selections a compatible sheet starts its review with: the values recovered from its fields.</summary>
    private ChoiceValue Initial(PdfInspection inspection)
    {
        var data = inspection.ExtractedData; var identity = data["identity"]; var className = Exact(Text(identity?["class_name"]), catalog.ClassRecords.Select(c => c.Name));
        var spellNames = catalog.SpellRecords.Select(s => s.Name).ToArray(); var recovered = new List<string>(); var unmatched = new List<string>();
        foreach (var name in (data["spells"]?["by_level"] as JsonObject ?? new()).SelectMany(p => Strings(p.Value)))
        { var match = Exact(name, spellNames); (match.Length > 0 ? recovered : unmatched).Add(match.Length > 0 ? match : name); }
        var cantrips = recovered.Where(n => catalog.Spell(n)!.Level == 0).Distinct(StringComparer.Ordinal).ToList();
        var leveled = recovered.Except(cantrips).ToList();
        var level = Math.Clamp(Integer(identity?["level"]) ?? 1, 1, 20);
        var species = Exact(Text(identity?["species"]), catalog.SpeciesRecords.Select(s => s.Name)); var background = Exact(Text(identity?["background"]), catalog.BackgroundRecords.Select(b => b.Name));
        var seed = Template().Build with { ClassName = className, Level = level, Species = species, Background = background };
        var known = characters.Describe(new DraftCharacter(seed, []), false, FoundValues.None).Definitions.Any(d => d.Path.SequenceEqual(new[] { "spells", "known" }) && d.Choose > 0);
        var leveledKey = className == "Wizard" ? "spellbook" : known ? "known" : "prepared";
        var spells = Record(("cantrips", Names(cantrips)), ("prepared", Names(leveledKey == "prepared" ? leveled : [])), ("known", Names(leveledKey == "known" ? leveled : [])), ("spellbook", Names(leveledKey == "spellbook" ? leveled : [])));
        var gearText = Text(data["equipment"]?["raw"]); var gear = RecoverEquipment(gearText, catalog.Equipment);
        var languageText = Regex.Match(Text(data["presentation"]?["proficiencies_and_languages"]), @"(?im)^Languages?\s*:\s*([\s\S]*?)(?=^[\p{L}][\p{L} ]*:\s|\z)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var languages = languageText.Success ? Regex.Split(languageText.Groups[1].Value, "[,;]").Select(s => Regex.Replace(s, @"\s+", " ").Trim()).Where(s => s.Length > 0 && s.Length <= 200).Take(100) : [];
        var features = Text(data["presentation"]?["features"]) + "\n" + Text(data["presentation"]?["additional_features"]);
        var feats = catalog.FeatRecords.Where(f => f.Name.Length > 0 && Present(features, f.Name)).ToArray();
        var masteries = new List<string>(); var masteryLine = Regex.Match(features, @"Weapon Master(?:y|ies)\s*:\s*([^\n]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (masteryLine.Success) foreach (var value in Regex.Split(masteryLine.Groups[1].Value, "[,;]")) { var match = Exact(value, catalog.WeaponRecords.Select(w => w.Name)); if (match.Length > 0) masteries.Add(match); }
        static ChoiceValue Evidence(JsonNode? node) => ChoiceReading.FromCatalog(node);
        var empty = ChoiceValue.Record([]);
        return Record(("name", ChoiceValue.Of(Text(identity?["name"]).Length > 0 ? Text(identity?["name"]) : Text(identity?["alternate_name"]))), ("class_name", ChoiceValue.Of(className)), ("level", ChoiceValue.Of(level)),
            ("species", ChoiceValue.Of(species)), ("background", ChoiceValue.Of(background)), ("background_primary_ability", ChoiceValue.Of("")), ("background_secondary_ability", ChoiceValue.Of("")),
            ("ability_scores", data["abilities"] is JsonObject abilities ? Evidence(abilities) : empty), ("ability_scores_confirmed", ChoiceValue.Of(false)), ("subclass", ChoiceValue.Of("")),
            ("class_skills", Names([])), ("class_options", empty), ("species_options", empty), ("origin_feat_choices", empty), ("expertise", Names([])), ("origin_languages", Names([])), ("languages", Names(languages)),
            ("general_feat_ids", Names(feats.Select(f => f.Id))), ("feat_choices", empty), ("feats_confirmed", ChoiceValue.Of(feats.Length == 0)), ("weapon_masteries", Names(masteries)), ("masteries_confirmed", ChoiceValue.Of(masteries.Count == 0)),
            ("spells", spells), ("spells_confirmed", ChoiceValue.Of(recovered.Count == 0 && unmatched.Count == 0)),
            ("custom_equipment", Names(gear.Recovered.Concat(gear.Residual))), ("include_default_equipment", ChoiceValue.Of(false)), ("equipment_confirmed", ChoiceValue.Of(gearText.Length == 0)), ("preserve_observed_maximum_hp", ChoiceValue.Of(true)),
            ("observed_maximum_hp", Evidence(data["hit_points"]?["maximum"])), ("observed_current_hp", Evidence(data["hit_points"]?["current"])), ("observed_temporary_hp", Evidence(data["hit_points"]?["temporary"])),
            ("recovered", Record(("spells", Names(recovered)), ("unmatched_spells", Names(unmatched)), ("equipment", Names(gear.Recovered)), ("equipment_residual", Names(gear.Residual)), ("feat_names", Names(feats.Select(f => f.Name))), ("weapon_masteries", Names(masteries)))));
    }

    /// <summary>A new character's starting draft, from Herohold's character template.</summary>
    private static CharacterDraft Template()
    {
        var template = CanonicalCharacterRules.Create();
        return CharacterDraft.FromDocument(template) with { Facts = WorkspaceProjection.ImportedFacts(template) };
    }

    /// <summary>The character a compatible sheet's confirmed selections describe, with the base scores that reproduce its final scores.</summary>
    private CharacterDraft Recover(ChoiceValue selected, PdfInspection inspection, List<ImportIssue> issues, CancellationToken ct)
    {
        var draft = Template();
        var className = Text(At(selected, "class_name")); var level = Math.Clamp(Integer(At(selected, "level")) ?? 1, 1, 20);
        static Dictionary<string, ChoiceValue> SplitSkilled(Dictionary<string, ChoiceValue> origin)
        {
            if (!origin.TryGetValue("proficiencies", out var combined)) return origin;
            if (combined.Items is not { } items || items.Any(n => n.Text is not { } name || !name.StartsWith("Skill: ", StringComparison.Ordinal) && !name.StartsWith("Tool: ", StringComparison.Ordinal)))
                throw new PdfFormatException("invalid_review", "Choose supported Skilled proficiencies.");
            var names = Strings(combined); origin.Remove("proficiencies");
            origin["skills"] = Names(names.Where(n => n.StartsWith("Skill: ", StringComparison.Ordinal)).Select(n => n[7..]));
            origin["tools"] = Names(names.Where(n => n.StartsWith("Tool: ", StringComparison.Ordinal)).Select(n => n[6..]));
            return origin;
        }
        var classOptions = Fields(At(selected, "class_options")); classOptions["class_skills"] = At(selected, "class_skills") ?? Names([]);
        var backgroundOptions = Fields(At(selected, "background_options"));
        backgroundOptions["primary_ability"] = ChoiceValue.Of(Text(At(selected, "background_primary_ability"))); backgroundOptions["secondary_ability"] = ChoiceValue.Of(Text(At(selected, "background_secondary_ability")));
        var spells = At(selected, "spells");
        var build = draft.Build with
        {
            Name = Text(At(selected, "name")).Trim(), Species = Text(At(selected, "species")).Trim(), Background = Text(At(selected, "background")).Trim(), Subclass = Text(At(selected, "subclass")).Trim(),
            ClassName = className, Level = level, ClassLevels = new() { [className] = level },
            ClassOptions = classOptions, SpeciesOptions = Fields(At(selected, "species_options")), BackgroundOptions = backgroundOptions,
            OriginFeatChoices = SplitSkilled(Fields(At(selected, "origin_feat_choices"))),
            HeroholdOriginFeatChoices = Fields(At(selected, "folio_origin_feat_choices")).ToDictionary(p => p.Key, p => p.Value.Fields is null ? p.Value : ChoiceValue.Record(SplitSkilled(Fields(p.Value))), StringComparer.Ordinal),
            Spells = spells is null ? new() : new() { Cantrips = Optional(At(spells, "cantrips")), Known = Optional(At(spells, "known")), Prepared = Optional(At(spells, "prepared")), Spellbook = Optional(At(spells, "spellbook")) },
            WeaponMasteries = Strings(At(selected, "weapon_masteries")).Distinct(StringComparer.Ordinal).ToList(), OriginLanguages = Strings(At(selected, "origin_languages")).Distinct(StringComparer.Ordinal).ToList(),
            PdfPassthroughFields = inspection.PassthroughFields.ToDictionary(p => p.Key, p => ChoiceReading.FromCatalog(p.Value), StringComparer.Ordinal),
            Feats = [], AbilityScoreAdjustments = [], AbilityScoreMethod = "Manual entry",
            LevelHistory = [new() { ClassName = className, FromLevel = 0, ToLevel = level, TransactionId = "pdf_import_checkpoint", Source = "pdf_import_checkpoint" }]
        };
        var feats = new List<FeatFact>();
        foreach (var id in Strings(At(selected, "general_feat_ids")))
        {
            var feat = catalog.FeatRecords.FirstOrDefault(f => f.Id == id);
            if (feat is null) { issues.Add(Issue("feat_" + Slug(id) + "_unknown", "A selected feat is not in the active catalog.")); continue; }
            feats.Add(new() { InstanceId = "pdf_import_" + Slug(id), FeatId = id, Name = feat.Name, Source = new() { Type = "pdf_import_reconciliation" }, Choices = Fields(At(selected, "feat_choices", id)) });
        }
        build = build with { Feats = feats };
        // Shown with the sheet's own Expertise, the way the rules read a recovered character.
        var expertise = Strings(At(selected, "expertise")).Distinct(StringComparer.Ordinal).ToList(); var found = new FoundValues([], expertise, []);
        DraftCharacter Shown() => new(build, []);
        foreach (var feat in feats)
        {
            if (catalog.Class(className) is null) continue;
            var choices = new Dictionary<string, ChoiceValue>(feat.Choices, StringComparer.Ordinal) { ["apply_ability_increase"] = ChoiceValue.Of(true) };
            build = characters.RepairFeat(Shown(), found, feat.InstanceId, Record(("mode", ChoiceValue.Of("feat")), ("feat_id", ChoiceValue.Of(feat.FeatId)), ("feat_choices", ChoiceValue.Record(choices))));
            build = build with { AbilityScoreAdjustments = [.. (build.AbilityScoreAdjustments ?? []).Select(a => a.FeatInstanceId != feat.InstanceId ? a : a with { SourceId = "feat:" + feat.InstanceId, SourceName = feat.Name, SourceFeat = feat.FeatId })] };
        }
        var baseline = new Dictionary<Ability, int>(build.BaseAbilityScores ?? []); var observed = At(selected, "ability_scores");
        int Final(Ability ability) { build = build with { BaseAbilityScores = new(baseline) }; return characters.FeatContext(Shown(), false, found).Scores.GetValueOrDefault(ability.ToString(), 10); }
        foreach (var ability in Abilities)
        {
            ct.ThrowIfCancellationRequested(); var final = Integer(At(observed ?? new(), ability.ToString()));
            if (final is null) { issues.Add(Issue("ability_" + Slug(ability.ToString()) + "_missing", ability + " needs a confirmed final score.")); continue; }
            if (final is < 1 or > 30) { issues.Add(Issue("ability_" + Slug(ability.ToString()) + "_cannot_reproduce", ability + " must be between 1 and 30.")); continue; }
            if (catalog.Class(className) is null) { baseline[ability] = final.Value; continue; }
            baseline[ability] = 10;
            var preferred = final.Value - (Final(ability) - 10);
            // Ask the same native ability rules used by creation; never infer hidden ASI/roll history.
            var found10 = false;
            foreach (var value in Enumerable.Range(1, 30).OrderBy(v => Math.Abs(v - preferred)))
            {
                baseline[ability] = value;
                if (Final(ability) != final.Value) continue;
                found10 = true; break;
            }
            if (!found10) { baseline[ability] = final.Value; issues.Add(Issue("ability_" + Slug(ability.ToString()) + "_cannot_reproduce", ability + " cannot be reproduced by the current source choices.", observed: At(observed!, ability.ToString())!.Display)); }
        }
        build = build with { BaseAbilityScores = baseline };
        var recovered = draft with { Build = build, IncludeDefaultEquipment = Flag(At(selected, "include_default_equipment")), CustomEquipment = Strings(At(selected, "custom_equipment")).Distinct(StringComparer.Ordinal).ToList() };
        // The sheet's Expertise and languages are what it shows; those the build does not give were entered by hand.
        var languages = Strings(At(selected, "languages")).Distinct(StringComparer.Ordinal).ToList();
        return characters.Import(recovered, new() { Proficiencies = new() { Skills = [], Saves = [], Expertise = expertise, Languages = languages, Scores = Abilities.ToDictionary(a => a, _ => 10) }, Expertise = expertise });
    }

    /// <summary>The builder's choices in review terms: each edits a recovered selection, plus the review's own confirmations.</summary>
    private List<BuilderChoice> Choices(CharacterCalculation calculation, ChoiceValue selected)
    {
        var translated = new List<BuilderChoice>();
        foreach (var definition in calculation.Choices)
        {
            if (definition.Id.StartsWith("existing_feat:", StringComparison.Ordinal)) continue;
            var d = BuilderChoices.From(definition); var path = definition.Path.ToArray(); string[] mapped = path;
            if (path.SequenceEqual(new[] { "class" })) mapped = ["class_name"];
            else if (path.SequenceEqual(new[] { "base_ability_scores" })) { mapped = ["ability_scores"]; d = d with { Label = "Final ability scores observed on the PDF" }; }
            else if (path.SequenceEqual(new[] { "class_options", "class_skills" })) mapped = ["class_skills"];
            else if (path.SequenceEqual(new[] { "class_options", "equipment_package" }) || path.SequenceEqual(new[] { "background_options", "equipment_package" })) continue;
            else if (path.Length == 2 && path[0] == "background_options" && path[1] is "primary_ability" or "secondary_ability") mapped = ["background_" + path[1]];
            translated.Add(d with { Id = "import_" + d.Id, Path = ["import_selections", .. mapped], Value = At(selected, mapped) ?? d.Value });
        }
        void Add(string id, string label, string type, string[] path, string section, bool required = true, int choose = 1, IEnumerable<string>? options = null, int? minimum = null, int? maximum = null)
        {
            var value = At(selected, path);
            var listed = (options ?? []).Distinct().Select(s => new BuilderOption { Value = ChoiceValue.Of(s), Label = s }).ToList();
            translated.Add(new()
            {
                Id = id, Label = label, Type = type, Section = section, Path = ["import_selections", .. path], Choose = choose, Required = required, Options = listed, FeatOptions = listed, Minimum = minimum, Maximum = maximum,
                Value = value ?? (type == "boolean" ? ChoiceValue.Of(false) : type == "multi" ? ChoiceValue.ListOf([]) : new()),
                Status = required && (value is null || type == "boolean" && value.Flag != true) ? "unresolved" : "resolved"
            });
        }
        Add("import_level", "Recovered character level", "number", ["level"], "identity", minimum: 1, maximum: 20);
        Add("import_final_scores_confirmed", "I confirmed the final scores and reconstructed baseline", "boolean", ["ability_scores_confirmed"], "abilities");
        Add("import_equipment", "Recovered and additional equipment", "multi", ["custom_equipment"], "equipment", false, 500, catalog.Equipment.Concat(Strings(At(selected, "custom_equipment"))).Order());
        foreach (var flag in new[] { ("equipment", "I reviewed recovered and unmatched equipment", "equipment"), ("spells", "I reviewed recovered spells and their sources", "spells"), ("masteries", "I reviewed Weapon Mastery choices", "equipment"), ("feats", "I reviewed feats and their required choices", "feats") }) Add("import_" + flag.Item1 + "_confirmed", flag.Item2, "boolean", [flag.Item1 + "_confirmed"], flag.Item3);
        Add("import_preserve_maximum_hp", "Preserve the PDF maximum HP with an explained adjustment", "boolean", ["preserve_observed_maximum_hp"], "health", false);
        foreach (var field in new[] { "maximum", "current", "temporary" }) Add("import_observed_" + field + "_hp", "PDF " + field + " HP", "number", ["observed_" + field + "_hp"], "health", false, minimum: 0, maximum: 100000);
        Add("import_general_feats", "Recovered general feats", "multi", ["general_feat_ids"], "feats", false, 20);
        var feats = catalog.FeatRecords.Select(f => new BuilderOption { Value = ChoiceValue.Of(f.Id), Label = f.Name, Description = f.Description, Source = f.Source }).ToList();
        translated[^1] = translated[^1] with { Options = feats, FeatOptions = feats };
        if (calculation.KnownClass)
        {
            var chosen = Strings(At(selected, "general_feat_ids"));
            foreach (var composite in calculation.Choices.Where(d => d.Id.StartsWith("existing_feat:", StringComparison.Ordinal)).Select(BuilderChoices.From))
            {
                var feat = composite.FeatOptions.Count == 1 ? composite.FeatOptions[0] : null;
                var id = feat?.Value.Text ?? ""; if (feat is null || !chosen.Contains(id)) continue;
                foreach (var field in feat.Children.Where(f => f.Id != "apply_ability_increase"))
                {
                    var relative = field.Path.SequenceEqual(new[] { "feat_ability_choice" }) ? ["ability"] : field.Path.FirstOrDefault() == "feat_choices" ? field.Path.Skip(1).ToArray() : field.Path.ToArray();
                    translated.Add(field with
                    {
                        Id = "import_" + id + "_" + field.Id, Label = feat.Label + ": " + field.Label, Path = ["import_selections", "feat_choices", id, .. relative],
                        Value = At(selected, ["feat_choices", id, .. relative]) ?? field.Value
                    });
                }
            }
        }
        var unmatched = Strings(At(selected, "recovered", "unmatched_spells"));
        if (unmatched.Count > 0) Add("import_unmatched_spells", "Remove unmatched entries after choosing supported replacements", "multi", ["recovered", "unmatched_spells"], "spells", false, 500, unmatched);
        var languages = Strings(At(selected, "languages"));
        if (languages.Count > 0) Add("import_recovered_languages", "Recovered language entries", "multi", ["languages"], "origin", false, 100, languages);
        return translated;
    }

    /// <summary>The sheet's own numbers beside those the rules calculated.</summary>
    private static List<(string Key, string Label, int? Observed, int? Calculated)> Comparisons(CharacterCalculation c, JsonObject data)
    {
        int? Known(int value) => c.KnownClass ? value : null;
        var result = new List<(string, string, int?, int?)>
        {
            ("maximum_hp", "Maximum HP", Integer(data["hit_points"]?["maximum"]), c.MaximumHp), ("armor_class", "Armor Class", Integer(data["comparison"]?["armor_class"]), Known(c.ArmorClass)),
            ("initiative", "Initiative", Integer(data["comparison"]?["initiative"]), Known(c.Initiative)), ("proficiency_bonus", "Proficiency bonus", Integer(data["comparison"]?["proficiency_bonus"]), Known(CharacterCalculations.ProficiencyBonus(c.Build.Level))),
            ("passive_perception", "Passive Perception", Integer(data["comparison"]?["passive_perception"]), 10 + (c.KnownClass ? c.SkillBonuses.GetValueOrDefault("Perception") : 0))
        };
        foreach (var ability in Abilities)
        {
            result.Add(("ability_score_" + Slug(ability.ToString()), ability + " score", Integer(data["abilities"]?[ability.ToString()]), c.KnownClass ? c.Scores.GetValueOrDefault(ability) : null));
            result.Add(("ability_modifier_" + Slug(ability.ToString()), ability + " modifier", Integer(data["comparison"]?["ability_modifiers"]?[ability.ToString()]), c.KnownClass ? c.Modifiers.GetValueOrDefault(ability) : null));
        }
        return result;
    }
    private static string Exact(string name, IEnumerable<string> names) => names.Where(n => n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).Distinct().SingleOrDefault() ?? "";
    private static void ValidateSelections(ChoiceValue selected)
    {
        if (selected.Fields is null) throw new PdfFormatException("invalid_review", "PDF review selections must be named values.");
        foreach (var key in new[] { "class_options", "species_options", "background_options", "origin_feat_choices", "folio_origin_feat_choices", "spells", "feat_choices", "ability_scores", "recovered" })
            if (At(selected, key) is { Fields: null }) throw new PdfFormatException("invalid_review", "PDF review " + key + " must be an object.");
        foreach (var key in new[] { "general_feat_ids", "class_skills", "weapon_masteries", "custom_equipment", "expertise", "origin_languages", "languages" })
        {
            if (At(selected, key) is not { } values) continue;
            if (values.Items is not { } items || items.Count > (key == "general_feat_ids" ? 20 : key is "languages" or "origin_languages" ? 100 : 500) || items.Any(n => n.Text is not { } text || text.Length > (key is "languages" or "origin_languages" ? 200 : 2000)) || Strings(values).Distinct(StringComparer.Ordinal).Count() != items.Count)
                throw new PdfFormatException("invalid_review", "Choose a bounded list of distinct supported " + key.Replace('_', ' ') + ".");
        }
        foreach (var key in new[] { "ability_scores_confirmed", "equipment_confirmed", "spells_confirmed", "feats_confirmed", "masteries_confirmed", "include_default_equipment", "preserve_observed_maximum_hp" })
            if (At(selected, key) is { Flag: null }) throw new PdfFormatException("invalid_review", "PDF confirmations must be true or false.");
    }
    private static bool Present(string text, string name) => Regex.IsMatch(text, @"(?<![\w'])" + Regex.Escape(name) + @"(?![\w'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static (List<string> Recovered, List<string> Residual) RecoverEquipment(string text, IReadOnlyList<string> equipment)
    {
        var recovered = new List<string>(); var residual = text;
        foreach (var name in equipment.Where(n => n.Length > 0).Distinct().OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.OrdinalIgnoreCase))
        { var pattern = @"(?<![\w'])" + Regex.Escape(name) + @"(?![\w'])"; if (!Regex.IsMatch(residual, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) continue; recovered.Add(name); residual = Regex.Replace(residual, pattern, " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
        residual = Regex.Replace(residual, @"\bx\s*\d+\b|\(\s*\d+\s*\)", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return (recovered, Regex.Split(residual, @"[,;\n]+").Select(s => Regex.Replace(s, @"\s+", " ").Trim(' ', '.', ':', '-')).Where(s => s.Length > 0).Distinct().ToList());
    }
}
