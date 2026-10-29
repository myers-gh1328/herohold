using System.Text.Json;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>Validates owned feat selections without treating the feat's own grants as prerequisites.</summary>
public sealed class ExistingFeatValidationRules(RulesCatalogSnapshot catalog, FeatSelectionRules selectionRules)
{
    private readonly IReadOnlyList<CatalogFeat> definitions = selectionRules.AvailableDefinitions;
    /// <summary>Gives each recognised feat its catalog name and id.</summary>
    public BuildFacts NormalizeIdentities(BuildFacts build)
    {
        Bound(build);
        if (build.Feats is not { } feats) return build;
        return build with
        {
            Feats = [.. feats.Select(row =>
            {
                var definition = Find(row); if (definition is null || Conflicts(row, definition)) return row;
                return row with { Name = definition.Name, FeatId = row.FeatId.Length == 0 ? definition.Id : row.FeatId };
            })]
        };
    }
    private readonly Lazy<CharacterBuildCatalog> build = new(() => CharacterBuildCatalog.From(
        ClassProgressionCatalog.From(catalog.ClassRecords), catalog));
    private static BuildIssue Issue(string instance, string code, string message, BuildIssueSeverity severity = BuildIssueSeverity.Error, string? key = null) =>
        new(code, key is null ? ["general_feats", instance] : ["general_feats", instance, "choices", key], message, severity);
    private CatalogFeat? Find(FeatFact f) => definitions.FirstOrDefault(d => d.Id.Equals(f.FeatId, StringComparison.OrdinalIgnoreCase))
        ?? definitions.FirstOrDefault(d => d.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase));
    private static bool Conflicts(FeatFact f, CatalogFeat definition)
        => f.FeatId.Length > 0 && !f.FeatId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)
        || f.Name.Length > 0 && !f.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase);
    private static int? Taken(FeatFact f) => f.LevelTaken ?? (f.Source.Type == "class_level" ? f.Source.Level : null);
    private static IEnumerable<AbilityScoreAdjustmentFact> Links(BuildFacts c, string id) => (c.AbilityScoreAdjustments ?? []).Where(a => a.FeatInstanceId == id);
    private static void Bound(BuildFacts c)
    {
        if (c.Feats is { Count: > 100 } || JsonSerializer.Serialize(c.Feats).Length > 8 * 1024 * 1024) throw new ArgumentException("The feat history is too large.");
    }
    private static Dictionary<string, int> Increases(IReadOnlyDictionary<Ability, int> adjustments) => adjustments.ToDictionary(p => p.Key.ToString(), p => p.Value);
    private static bool SameIncreases(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);
    private FeatRuleContext Before(BuildFacts c, FeatFact f, FeatRuleContext current)
    {
        var owner = "feat:" + f.InstanceId; var level = Taken(f); var scores = current.Scores;
        var horizon = level ?? Find(f)?.Prerequisites.MinimumLevel ?? 1;
        var resilient = Find(f)?.Name == "Resilient";
        // Rebuild capped increases from their sources; subtracting requested increases is wrong at a cap.
        if (level is >= 1 and <= 20 && c.BaseAbilityScores is not null)
        {
            var prior = c with
            {
                Level = level.Value,
                Feats = [.. (c.Feats ?? []).Where(row => row.InstanceId != f.InstanceId && (Taken(row) is not int at || at <= level))],
                AbilityScoreAdjustments = [.. (c.AbilityScoreAdjustments ?? []).Where(a => a.FeatInstanceId != f.InstanceId && (a.SourceLevel ?? a.Source?.Level ?? 1) <= level)]
            };
            try { scores = build.Value.Derive(prior, [], true).Scores.Scores.ToDictionary(p => p.Key.ToString(), p => p.Value); }
            catch (ArgumentException) { /* Incomplete legacy sources cannot establish an earlier score. */ }
        }
        return current with
        {
            Level = level is >= 1 and <= 20 ? level.Value : current.Level,
            Scores = scores, EditingSourceId = owner,
            ProficiencySources = current.ProficiencySources.Where(p => p.SourceId != owner
                && (!resilient || p.Kind != ProficiencySourceKind.Save || current.SourceLevels.ContainsKey(p.SourceId))
                && (!current.SourceLevels.TryGetValue(p.SourceId, out var at) || at <= horizon)).ToArray(),
            OwnedFeats = current.OwnedFeats.Where(o => o.SourceId == owner || level is null || !current.SourceLevels.TryGetValue(o.SourceId, out var at) || at <= level).ToArray()
        };
    }
    private static Dictionary<string, int> Allocation(CatalogFeat definition, IReadOnlyDictionary<string, ChoiceValue> choices, BuildFacts c, string instance)
    {
        var increase = definition.AbilityIncrease; if (increase is null) return [];
        if (increase.Mode == "multi_mode")
        {
            if (choices.Get("ability_increases")?.Fields is { } explicitAllocation) return explicitAllocation.Where(p => p.Value.Number is not null).ToDictionary(p => p.Key, p => p.Value.Number!.Value);
            var primary = ChoiceReading.TextOf(choices.Get("primary_ability")); var secondary = ChoiceReading.TextOf(choices.Get("secondary_ability"));
            if (primary.Length > 0)
            {
                var mode = ChoiceReading.TextOf(choices.Get("allocation_mode"));
                if (mode == "split") return secondary.Length > 0 && primary != secondary ? new() { [primary] = 1, [secondary] = 1 } : [];
                return mode == "single" ? new() { [primary] = 2 } : [];
            }
            return Links(c, instance).FirstOrDefault() is { } linked ? Increases(linked.Adjustments) : [];
        }
        var ability = ChoiceReading.TextOf(choices.Get("ability"));
        if (ability.Length == 0 && increase.Mode == "fixed") ability = increase.From.FirstOrDefault() ?? "";
        return ability.Length == 0 ? [] : new() { [ability] = increase.Amount ?? 1 };
    }
    private Dictionary<string, ChoiceValue> ChildChoices(CatalogFeat definition, IReadOnlyDictionary<string, ChoiceValue> choices, FeatRuleContext context)
    {
        var allowed = selectionRules.DescribeChoices(definition.Id, context).Select(f => f.Key).ToHashSet();
        return NormalizeChoices(definition, choices).Where(p => allowed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    }
    private static Dictionary<string, ChoiceValue> NormalizeChoices(CatalogFeat definition, IReadOnlyDictionary<string, ChoiceValue> choices)
    {
        var normalized = new Dictionary<string, ChoiceValue>(choices, StringComparer.Ordinal);
        if (definition.Name == "Skilled" && !normalized.ContainsKey("proficiencies"))
            normalized["proficiencies"] = ChoiceValue.ListOf(ChoiceReading.Names(choices.Get("skills")).Select(n => "Skill: " + n).Concat(ChoiceReading.Names(choices.Get("tools")).Select(n => "Tool: " + n)).Select(ChoiceValue.Of));
        return normalized;
    }
    private HashSet<string> AllowedKeys(CatalogFeat definition, FeatRuleContext context)
    {
        var allowed = selectionRules.DescribeChoices(definition.Id, context).Select(field => field.Key).ToHashSet();
        if (definition.AbilityIncrease is not null) allowed.Add("ability");
        if (definition.Name == "Skilled") allowed.UnionWith(["skills", "tools"]);
        if (definition.AbilityIncrease?.Mode == "multi_mode") allowed.UnionWith(["ability_increases", "allocation_mode", "primary_ability", "secondary_ability"]);
        return allowed;
    }
    public IReadOnlyList<BuildIssue> Validate(BuildFacts build, FeatRuleContext current)
    {
        Bound(build); var result = new List<BuildIssue>();
        var seen = new HashSet<string>(); var instances = new HashSet<string>();
        foreach (var f in build.Feats ?? [])
        {
            var id = f.InstanceId; var definition = Find(f); var taken = Taken(f);
            if (id.Length == 0 || id.Length > 200 || !instances.Add(id)) result.Add(Issue(id, "feat_identity", "Each feat needs a unique stable identity."));
            if (definition is null) { result.Add(Issue(id, "unknown_feat", "This feat has no supported rules definition; its history is preserved for review.", BuildIssueSeverity.Warning)); continue; }
            var featId = definition.Id; var name = definition.Name;
            if (name == "Resilient" && current.ProficiencySources.Any(p => p.Kind == ProficiencySourceKind.Save && p.SourceId != "feat:" + id && !current.SourceLevels.ContainsKey(p.SourceId)))
                result.Add(Issue(id, "unknown_source_history", "An imported saving throw proficiency has no gain level; its overlap with this historical feat needs review.", BuildIssueSeverity.Warning));
            if (Conflicts(f, definition))
                result.Add(Issue(id, "feat_identity", "The feat name and identity do not describe the same feat."));
            if (!definition.Repeatable && !seen.Add(featId)) result.Add(Issue(id, "duplicate_feat", "This feat cannot be taken more than once."));
            if (taken is null) result.Add(Issue(id, "unknown_history", "The original level and prerequisites cannot be verified from this feat's history.", BuildIssueSeverity.Warning));
            else if (taken is < 1 or > 20 || taken > current.Level) result.Add(Issue(id, "invalid_history", "The feat's gain level is invalid."));
            var context = Before(build, f, current); var choices = f.Choices;
            var allowedKeys = AllowedKeys(definition, context);
            foreach (var key in choices.Keys.Where(key => !allowedKeys.Contains(key))) result.Add(Issue(id, "unknown_choice", "This choice does not belong to the selected feat.", key: key));
            var linked = Links(build, id).ToArray();
            if (linked.Length > 1) result.Add(Issue(id, "ambiguous_ability_increase", "More than one ability increase claims this feat; review its history before applying changes."));
            var allocation = Allocation(definition, choices, build, id);
            var expectedMaximum = definition.AbilityIncrease?.Maximum ?? 20;
            if (linked.Length == 1 && (!SameIncreases(Increases(linked[0].Adjustments), allocation) || (linked[0].Maximum ?? 20) != expectedMaximum))
                result.Add(Issue(id, "ability_increase_mismatch", "The recorded ability increase does not match this feat's choices and limit; repair the feat before adoption."));
            // Ritual Caster's owned spell count grows with current proficiency bonus.
            var validationContext = name == "Ritual Caster" ? context with { Level = current.Level } : context;
            var verdict = selectionRules.ValidateSelection(featId, validationContext, ChildChoices(definition, choices, validationContext), allocation);
            foreach (var issue in verdict.Issues)
            {
                if (issue.Code == "use_asi_option") continue; // Historical ASI records use explicit allocations.
                result.Add(Issue(id, issue.Code == "already_owned" ? "duplicate_feat" : issue.Code, issue.Message,
                    taken is null && issue.Code is "prerequisites" or "save_already_proficient" ? BuildIssueSeverity.Warning : issue.Severity,
                    issue.Path.Count > 2 ? issue.Path[^1] : null));
            }
            if (definition.AbilityIncrease is not null && linked.Length == 0 && f.AbilityIncreaseInBase != true)
                result.Add(Issue(id, "unlinked_ability_increase", "Confirm whether the base scores already include this feat's increase before adding it.", BuildIssueSeverity.Warning));
        }
        return result.AsReadOnly();
    }
    public IReadOnlyList<ExistingFeatChoice> DescribeChoices(BuildFacts build, FeatRuleContext current)
    {
        Bound(build); var issues = Validate(build, current); var result = new List<ExistingFeatChoice>();
        foreach (var f in (build.Feats ?? []).GroupBy(f => f.InstanceId).Where(g => g.Count() == 1).Select(g => g.Single()))
        {
            var definition = Find(f); if (definition is null) continue;
            var id = f.InstanceId; var featId = definition.Id; var context = Before(build, f, current); var choices = NormalizeChoices(definition, f.Choices); var fields = new List<BuilderChoiceDefinition>();
            void Add(string key, string label, string[] path, IEnumerable<string> options, ChoiceValue? value, int choose = 1, bool required = true, BuildChoiceMode? mode = null)
            {
                var opts = options.Distinct().ToArray(); var selected = value?.Items is { } a ? a.Select(v => v.Text ?? "").ToArray() : new[] { value?.Text ?? "" };
                fields.Add(new() { Id = key, Label = label, Section = "feats", Path = path, Mode = mode ?? (choose == 1 ? BuildChoiceMode.Single : BuildChoiceMode.Multi), Choose = choose, Required = required,
                    Options = opts.Select(o => new BuildChoiceOption { Value = ChoiceValue.Of(o), Label = o }).ToArray(), PreservedSelection = value,
                    Status = selected.Length == choose && selected.Distinct().Count() == choose && selected.All(opts.Contains) ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved });
            }
            string Picked(string key) => ChoiceReading.TextOf(choices.Get(key));
            var increase = definition.AbilityIncrease;
            if (increase?.Mode == "choose")
            {
                var abilities = increase.From.AsEnumerable();
                if (definition.Name == "Resilient") abilities = abilities.Where(a => !context.ProficiencySources.Any(p => p.Kind == ProficiencySourceKind.Save && p.Target == a));
                Add("ability", "Feat ability increase", ["feat_ability_choice"], abilities, choices.Get("ability"));
            }
            if (increase?.Mode == "multi_mode")
            {
                Add("allocation_mode", "Ability increase", ["feat_choices", "allocation_mode"], ["single", "split"], choices.Get("allocation_mode"));
                Add("primary_ability", "First ability", ["feat_choices", "primary_ability"], Enum.GetNames<Ability>(), choices.Get("primary_ability"));
                if (Picked("allocation_mode") == "split") Add("secondary_ability", "Second ability", ["feat_choices", "secondary_ability"], Enum.GetNames<Ability>().Where(a => a != Picked("primary_ability")), choices.Get("secondary_ability"));
            }
            var schemaContext = definition.Name == "Ritual Caster" ? context with { Level = current.Level } : context;
            foreach (var field in selectionRules.DescribeChoices(featId, schemaContext))
            {
                var options = field.Options.AsEnumerable();
                if (definition.Name == "Skill Expert" && field.Key == "expertise" && Picked("skill_proficiency").Length > 0)
                    options = options.Append(Picked("skill_proficiency"));
                Add(field.Key, field.Label, ["feat_choices", field.Key], options, choices.Get(field.Key), field.Choose, mode: field.Mode);
            }
            if (increase is not null && !Links(build, id).Any()) fields.Add(new() { Id = "apply_ability_increase", Label = "Do your base scores already include this feat's increase?", Section = "feats", Path = ["feat_choices", "apply_ability_increase"], Required = false,
                Mode = BuildChoiceMode.Single, Options = [new() { Value = ChoiceValue.Of(false), Label = "Yes, keep my base scores" }, new() { Value = ChoiceValue.Of(true), Label = "No, add the increase" }] });
            result.Add(new() { InstanceId = id, FeatId = featId, Name = definition.Name, KnownTakenLevel = Taken(f), Fields = fields.AsReadOnly(), Issues = issues.Where(i => i.Path.Count > 1 && i.Path[1] == id).ToArray() });
        }
        return result.AsReadOnly();
    }
    /// <summary>Repairs one owned feat's choices; choices it no longer offers are kept in its retired history.</summary>
    public BuildFacts ApplyChoices(BuildFacts build, string instanceId, IReadOnlyDictionary<string, ChoiceValue> choices)
    {
        Bound(build); if (JsonSerializer.Serialize(choices).Length > 65536) throw new ArgumentException("The feat choices are too large.");
        var feats = (build.Feats ?? []).ToList(); var index = feats.FindIndex(f => f.InstanceId == instanceId);
        if (index < 0 || feats.Count(f => f.InstanceId == instanceId) != 1) throw new ArgumentException("Choose one existing feat instance.");
        var feat = feats[index]; var definition = Find(feat) ?? throw new ArgumentException("This feat has no supported choice definition.");
        var updated = new Dictionary<string, ChoiceValue>(feat.Choices, StringComparer.Ordinal);
        var allowed = AllowedKeys(definition, new() { Level = build.Level });
        var unsupported = updated.Where(p => !allowed.Contains(p.Key)).ToArray();
        var history = feat.LegacyChoiceHistory;
        if (unsupported.Length > 0)
        {
            history = [.. history ?? [], unsupported.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)];
            foreach (var pair in unsupported) updated.Remove(pair.Key);
        }
        foreach (var pair in choices) if (pair.Key != "apply_ability_increase") updated[pair.Key] = pair.Value;
        feat = feat with { Choices = updated, LegacyChoiceHistory = history, FeatId = feat.FeatId.Length == 0 ? definition.Id : feat.FeatId };
        var result = build;
        var allocation = Allocation(definition, updated, result, instanceId); var increase = definition.AbilityIncrease;
        var modes = increase?.Mode == "multi_mode" ? increase.Modes.ToArray() : increase is null ? [] : new[] { increase };
        bool Matches(FeatAbilityIncrease mode) => allocation.Count == (mode.Choose ?? 1) && allocation.All(p => mode.From.Contains(p.Key) && p.Value == (mode.Amount ?? 1));
        var adjustments = (result.AbilityScoreAdjustments ?? []).ToList();
        if (modes.Any(Matches))
        {
            var links = adjustments.Select((a, i) => (a, i)).Where(p => p.a.FeatInstanceId == instanceId).ToArray();
            var typed = allocation.ToDictionary(p => Enum.Parse<Ability>(p.Key), p => p.Value);
            if (links.Length == 1)
                adjustments[links[0].i] = links[0].a with { Adjustments = typed, Maximum = increase?.Maximum ?? 20 };
            else if (links.Length == 0 && choices.Get("apply_ability_increase")?.Flag == true)
                adjustments.Add(new() { Type = "ability_score_adjustment", SourceType = "feat", FeatInstanceId = instanceId, Source = feat.Source, Adjustments = typed, Maximum = increase?.Maximum ?? 20 });
        }
        if (choices.Get("apply_ability_increase")?.Flag is { } apply) feat = feat with { AbilityIncreaseInBase = !apply };
        feats[index] = feat;
        return NormalizeIdentities(result with { Feats = feats, AbilityScoreAdjustments = result.AbilityScoreAdjustments is null && adjustments.Count == 0 ? null : adjustments });
    }
}
