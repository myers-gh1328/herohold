using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography;
using Herohold.Core;
namespace Herohold.Rules;

public sealed partial class NativeLevelUpRules(RulesCatalogSnapshot catalog, ClassProgressionCatalog classes,
    FeatSelectionRules feats, CharacterEditRules edits, ICharacterDraftRules characters)
{
    private readonly IReadOnlyList<CatalogFeat> featRecords = ReadFeatDefinitions(catalog, feats);
    private static IReadOnlyList<CatalogFeat> ReadFeatDefinitions(RulesCatalogSnapshot catalog, FeatSelectionRules feats)
    { ArgumentNullException.ThrowIfNull(catalog); return feats.AvailableDefinitions; }
    private static readonly string[] Abilities = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];
    private static string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    private static string Text(ChoiceValue? value) => value?.Text ?? "";
    private static string[] Names(ChoiceValue? value) => value?.Items?.Select(v => v.Text ?? "").Where(s => s.Length > 0).ToArray() ?? [];
    private static ChoiceValue List(IEnumerable<string> values) => ChoiceValue.ListOf(values.Select(ChoiceValue.Of));
    private static string PathKey(IEnumerable<string> path) => string.Join("/", path);
    private static ChoiceValue? At(BuildFacts c, IReadOnlyList<string> path) => c.AsChoiceTree().At(path) is { IsNull: false } value ? value : null;
    private static BuildIssue Issue(string id, string code, string message) => new(code, ["level_up", id], message);
    private static string Fingerprint(BuildFacts c) => JsonSerializer.Serialize(c);
    private DraftCharacter With(DraftCharacter source, BuildFacts build) => source with { Build = build };
    private SettledBuild Calculate(DraftCharacter c) => characters.Recalculate(c);
    private DraftCharacter Settle(DraftCharacter c) => With(c, Calculate(c).Build);
    private CreationChoiceReport Describe(DraftCharacter c) => characters.Describe(c);
    private static LevelUpDraft Detach(LevelUpDraft d) => d with { Selections = new Dictionary<string, ChoiceValue>(d.Selections, StringComparer.Ordinal) };

    public LevelUpReview Start(DraftCharacter source, int targetLevel)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Build.ClassTracks is not null) throw new LevelUpException("class_selection_required", "Choose which class to advance.");
        var current = source.Build.Level;
        if (current is < 1 or >= 20 || targetLevel != current + 1) throw new LevelUpException("invalid_level", "Advance one supported character level at a time.");
        var canonical = ClassProgressionRules.AtLevel(classes, source.Build.ClassName, current, source.Build.Subclass);
        return Rebuild(source, new() { TransactionId = Guid.NewGuid().ToString("N"), SourceHash = SourceHash(source.Build, source.Equipment), ClassName = canonical.ClassName, CurrentLevel = current, TargetLevel = targetLevel }, new());
    }

    public LevelUpReview Choose(DraftCharacter source, LevelUpDraft draft, string choiceId, ChoiceValue value)
    {
        var current = Rebuild(source, draft, new());
        if (!current.PendingChoices.Any(c => c.Id == choiceId)) throw new LevelUpException("invalid_choice", "That choice is not active in this level-up draft.");
        var choices = new Dictionary<string, ChoiceValue>(draft.Selections, StringComparer.Ordinal) { [choiceId] = value };
        return Rebuild(source, draft with { Selections = choices }, new());
    }

    public LevelUpReview Preview(DraftCharacter source, LevelUpDraft draft, LevelUpHpOptions? hp = null) => Rebuild(source, draft, hp ?? new());

    public LevelUpReview Commit(DraftCharacter source, LevelUpDraft draft, LevelUpHpOptions? hp = null)
    {
        var review = Rebuild(source, draft, hp ?? new());
        if (!review.CanFinalize) throw new LevelUpException("incomplete_levelup", "Complete every required level-up choice before confirming.");
        var candidate = review.Build with
        {
            CompletedLevelUpTransactions = [.. review.Build.CompletedLevelUpTransactions ?? [], draft.TransactionId],
            LevelHistory = [.. review.Build.LevelHistory ?? [], new LevelUpFact
            {
                ClassName = draft.ClassName, FromLevel = draft.CurrentLevel, ToLevel = draft.TargetLevel, TransactionId = draft.TransactionId, HpIncrease = review.HpIncrease,
                AutomaticFeatures = [.. review.AutomaticFeatures], ResolvedChoices = [.. review.PendingChoices.Select(c => new ResolvedChoiceFact { Id = c.Id, Value = c.PreservedSelection ?? new() })]
            }]
        };
        return review with { Build = candidate, Draft = Detach(draft with { Completed = true }) };
    }

    private (CreationChoiceReport Report, int MaximumHp) Validate(DraftCharacter source, LevelUpDraft draft)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(draft);
        var build = source.Build;
        if (draft.Completed || (build.CompletedLevelUpTransactions ?? []).Contains(draft.TransactionId)) throw new LevelUpException("already_completed", "This level-up transaction has already been completed.");
        if (!Guid.TryParseExact(draft.TransactionId, "N", out _) || draft.SourceHash is null || draft.SourceHash.Length != 64 || string.IsNullOrWhiteSpace(draft.ClassName) || draft.Selections is null || draft.Selections.Count > 100)
            throw new LevelUpException("invalid_draft", "Start a new valid level-up draft.");
        if (draft.SourceHash != SourceHash(build, source.Equipment)) throw new LevelUpException("stale_draft", "The character changed. Start a fresh level-up draft.");
        if (draft.CurrentLevel != build.Level || draft.CurrentLevel is < 1 or >= 20 || draft.TargetLevel != draft.CurrentLevel + 1 || !draft.ClassName.Equals(build.ClassName, StringComparison.OrdinalIgnoreCase))
            throw new LevelUpException("invalid_draft", "The draft does not match this character and next level.");
        foreach (var item in draft.Selections)
            if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 200 || JsonSerializer.Serialize(item.Value).Length > 32768) throw new LevelUpException("invalid_draft", "A draft choice exceeds its supported bounds.");
        if ((build.ClassLevels ?? []).Any(p => !p.Key.Equals(draft.ClassName, StringComparison.OrdinalIgnoreCase) && p.Value > 0))
            throw new LevelUpException("unsupported_multiclass", "This level-up workflow supports one class.");
        var settled = Calculate(source);
        var validation = Describe(With(source, settled.Build));
        if (validation.Issues.Any(i => i.Severity == BuildIssueSeverity.Error) || validation.Definitions.Any(d => d.Status != BuildChoiceStatus.Resolved)) throw new LevelUpException("incomplete_character", "Complete the character's current required choices before leveling up.");
        return (validation, settled.MaximumHp);
    }

    private LevelUpReview Rebuild(DraftCharacter source, LevelUpDraft draft, LevelUpHpOptions hp)
    {
        if (draft.ClassTrackId.Length > 0) return RebuildTrack(source, draft, hp);
        var (sourceReport, previousMaximum) = Validate(source, draft);
        var target = ClassProgressionRules.AtLevel(classes, draft.ClassName, draft.TargetLevel, source.Build.Subclass);
        if (hp.Method is not (HitPointMethod.Fixed or HitPointMethod.Rolled) || hp.Method == HitPointMethod.Rolled && (hp.Roll is null || hp.Roll < 1 || hp.Roll > target.HitDie))
            throw new LevelUpException("invalid_hp", "Choose fixed HP or enter an unmodified Hit Die result within its range.");
        var baseGain = hp.Method == HitPointMethod.Rolled ? hp.Roll!.Value : HitPointRules.FixedHitDieGain(target.HitDie);
        var levels = new Dictionary<string, int>(source.Build.ClassLevels ?? [], StringComparer.Ordinal) { [draft.ClassName] = draft.TargetLevel };
        var candidate = Settle(With(source, source.Build with
        {
            Level = draft.TargetLevel, ClassLevels = levels,
            HpHistory = [.. source.Build.HpHistory ?? [], new HpGainFact { CharacterLevel = draft.TargetLevel, ClassName = draft.ClassName, Method = hp.Method == HitPointMethod.Rolled ? "rolled" : "fixed", BaseGain = baseGain }]
        }));
        var gains = target.ChoicesAtLevel.Where(f => f.Type == "asi_or_feat" || f.Name == "Epic Boon").ToArray();
        var gainIssues = new List<BuildIssue>();
        foreach (var gain in gains)
            if (draft.Selections.TryGetValue(gain.Id, out var selected)) candidate = ApplyImprovement(candidate, draft, gain.Id, selected, gainIssues);
        foreach (var ritual in RitualChoices(source, candidate, draft))
            if (ritual.Definition.Status == BuildChoiceStatus.Resolved)
                candidate = With(candidate, candidate.Build with
                {
                    Feats = [.. (candidate.Build.Feats ?? []).Select(f => f.InstanceId != ritual.InstanceId ? f
                        : f with { Choices = new Dictionary<string, ChoiceValue>(f.Choices, StringComparer.Ordinal) { ["spells"] = List(ritual.Existing.Concat(Names(ritual.Definition.PreservedSelection))) } })]
                });
        candidate = Settle(candidate);
        var sourceDefinitions = sourceReport.Definitions.ToDictionary(d => PathKey(d.Path), StringComparer.Ordinal);
        // Recompute dependent option universes after every parent change. Only paths from the
        // trusted description can become edits; the caller never supplies a path or rule.
        for (var pass = 0; pass < 8; pass++)
        {
            var before = Fingerprint(candidate.Build);
            foreach (var binding in Bindings(source, candidate, draft, sourceDefinitions))
                if (draft.Selections.TryGetValue(binding.Definition.Id, out var selected)) candidate = ApplyBinding(candidate, binding, selected);
            candidate = Settle(candidate);
            if (before == Fingerprint(candidate.Build)) break;
        }
        foreach (var replacement in ReplacementChoices(source, candidate, draft))
            if (replacement.Replace && replacement.Choices.All(c => c.Status == BuildChoiceStatus.Resolved))
            {
                var value = replacement.Scalar ? ChoiceValue.Of(replacement.With) : List(Names(At(candidate.Build, replacement.Path)).Where(s => s != replacement.From).Append(replacement.With));
                candidate = ApplyPath(candidate, replacement.Path, value);
            }
        var settled = Calculate(candidate); candidate = With(candidate, settled.Build);
        var report = Describe(candidate); var pending = Bindings(source, candidate, draft, sourceDefinitions).Select(b => b.Definition).ToList();
        pending.AddRange(RitualChoices(source, candidate, draft).Select(r => r.Definition));
        pending.AddRange(ReplacementChoices(source, candidate, draft).SelectMany(r => r.Choices));
        foreach (var gain in gains) pending.Add(ImprovementDefinition(candidate, draft, gain));
        var issues = report.Issues.Concat(gainIssues).Concat(pending.SelectMany(p => p.Issues)).DistinctBy(i => i.Code + "|" + PathKey(i.Path)).ToArray();
        var increase = settled.MaximumHp - previousMaximum;
        var progression = ClassProgressionRules.AtLevel(classes, draft.ClassName, draft.TargetLevel, candidate.Build.Subclass);
        var automatic = progression.FeaturesAtLevel.Concat(progression.SubclassFeaturesAtLevel).Where(name => name != "Ability Score Improvement" && name != "Epic Boon").Distinct(StringComparer.Ordinal).ToArray();
        return new() { Draft = Detach(draft), Build = candidate.Build, MaximumHp = settled.MaximumHp, PendingChoices = pending.AsReadOnly(), AutomaticFeatures = automatic, Issues = issues,
            CanFinalize = issues.All(i => i.Severity != BuildIssueSeverity.Error) && pending.All(p => p.Status == BuildChoiceStatus.Resolved), HitDie = target.HitDie, HpBaseGain = baseGain, HpIncrease = increase,
            ProficiencyBonusBefore = CharacterCalculations.ProficiencyBonus(draft.CurrentLevel), ProficiencyBonusAfter = CharacterCalculations.ProficiencyBonus(draft.TargetLevel) };
    }

    private sealed record Binding(BuilderChoiceDefinition Definition, string[] Existing, string[] RawPath);
    private sealed record RitualBinding(BuilderChoiceDefinition Definition, string InstanceId, string[] Existing);
    private sealed record ReplacementBinding(IReadOnlyList<BuilderChoiceDefinition> Choices, string[] Path, bool Replace, string From, string With, bool Scalar = false);

    private static BuildChoiceOption Named(string value, string label) => new() { Value = ChoiceValue.Of(value), Label = label };

    private IReadOnlyList<ReplacementBinding> ReplacementChoices(DraftCharacter source, DraftCharacter candidate, LevelUpDraft draft)
    {
        var result = new List<ReplacementBinding>(); var descriptions = Describe(candidate).Definitions;
        void Add(string[] path, string key, string label)
        {
            var existing = Names(At(source.Build, path)).Distinct(StringComparer.Ordinal).ToArray(); if (existing.Length == 0) return;
            var descriptor = descriptions.SingleOrDefault(d => d.Path.SequenceEqual(path)); if (descriptor is null) return;
            var prefix = draft.ClassName.ToLowerInvariant() + "_" + draft.TargetLevel + "_" + key;
            var controlId = prefix + "_replacement"; var fromId = prefix + "_replace_from"; var withId = prefix + "_replace_with";
            var mode = draft.Selections.TryGetValue(controlId, out var opted) ? Text(opted) : "keep";
            var choices = new List<BuilderChoiceDefinition>();
            BuilderChoiceDefinition Field(string id, string name, IEnumerable<(string Value, string Label)> options, string selected, bool required)
            {
                var items = options.ToArray(); var valid = items.Any(o => o.Value == selected);
                return new() { Id = id, Label = name, Path = [], Mode = BuildChoiceMode.Single, Section = "spells", Required = required,
                    Options = items.Select(o => Named(o.Value, o.Label)).ToArray(), PreservedSelection = ChoiceValue.Of(selected),
                    Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved,
                    Issues = valid ? System.Array.Empty<BuildIssue>() : new[] { Issue(id, "invalid_replacement", "Choose an eligible spell for the explicit replacement.") },
                    Details = new() { ReplacementPath = path, Replacement = true } };
            }
            choices.Add(Field(controlId, "Change one " + label + "?", [("keep", "Keep current spells"), ("replace", "Replace one spell")], mode, false));
            draft.Selections.TryGetValue(fromId, out var fromValue); draft.Selections.TryGetValue(withId, out var withValue);
            var from = Text(fromValue); var with = Text(withValue);
            if (mode == "replace")
            {
                var learningId = PublicId(descriptor, draft); draft.Selections.TryGetValue(learningId, out var learning);
                var protectedNames = existing.Concat(Names(learning)).ToHashSet(StringComparer.Ordinal);
                choices.Add(Field(fromId, "Spell to replace", existing.Select(s => (s, s)), from, true));
                var allowed = descriptor.Options.Where(o => !o.Disabled && !protectedNames.Contains(Text(o.Value))).Select(o => (Text(o.Value), o.Label));
                choices.Add(Field(withId, "Replacement spell", allowed, with, true));
            }
            result.Add(new(choices.AsReadOnly(), path, mode == "replace", from, with));
        }
        if (draft.ClassName is "Bard" or "Cleric" or "Druid" or "Sorcerer" or "Warlock") Add(["spells", "cantrips"], "cantrips", "cantrip");
        if (draft.ClassName is "Bard" or "Sorcerer" or "Warlock")
        {
            Add(["spells", "prepared"], "prepared", "prepared spell");
            Add(["spells", "known"], "known", "known spell");
        }
        var fightingStyle = Text(source.Build.ClassOptions.Get("fighting_style"));
        if (draft.ClassName == "Paladin" && fightingStyle == "Blessed Warrior") Add(["class_options", "blessed_warrior_cantrips"], "blessed_warrior_cantrips", "Blessed Warrior cantrip");
        if (draft.ClassName == "Ranger" && fightingStyle == "Druidic Warrior") Add(["class_options", "druidic_warrior_cantrips"], "druidic_warrior_cantrips", "Druidic Warrior cantrip");
        if (draft.ClassName == "Warlock")
        {
            var pools = new[] { 6, 7, 8, 9 }.Select(level => new { Level = level, Key = "mystic_arcanum_" + level })
                .Select(p => new { p.Level, p.Key, Existing = ChoiceReading.Names(source.Build.ClassOptions.Get(p.Key)).ToArray(), Definition = descriptions.SingleOrDefault(d => d.Path.SequenceEqual(new[] { "class_options", p.Key })) })
                .Where(p => p.Existing.Length == 1 && p.Definition is not null).ToArray();
            if (pools.Length > 0)
            {
                var prefix = "warlock_" + draft.TargetLevel + "_mystic_arcanum"; var controlId = prefix + "_replacement"; var withId = prefix + "_replace_with";
                draft.Selections.TryGetValue(controlId, out var option); var mode = option is null ? "keep" : Text(option);
                var selectedPool = pools.SingleOrDefault(p => p.Key == mode); var controlValid = mode == "keep" || selectedPool is not null;
                var choices = new List<BuilderChoiceDefinition>
                {
                    new() { Id = controlId, Label = "Change one Mystic Arcanum spell?", Mode = BuildChoiceMode.Single, Required = false, Section = "spells", PreservedSelection = ChoiceValue.Of(mode),
                        Options = new[] { Named("keep", "Keep current arcanum spells") }.Concat(pools.Select(p => Named(p.Key, "Replace " + p.Existing[0] + " (level " + p.Level + ")"))).ToArray(),
                        Status = controlValid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = controlValid ? [] : [Issue(controlId, "invalid_replacement", "Choose one existing arcanum source or keep all current spells.")] }
                };
                draft.Selections.TryGetValue(withId, out var withValue); var with = Text(withValue);
                if (selectedPool is not null)
                {
                    var options = selectedPool.Definition!.Options.Where(o => !o.Disabled && Text(o.Value) != selectedPool.Existing[0]).ToArray();
                    var valid = options.Any(o => Text(o.Value) == with);
                    choices.Add(new() { Id = withId, Label = "Replacement level " + selectedPool.Level + " Arcanum spell", Mode = BuildChoiceMode.Single, Section = "spells", Options = options, PreservedSelection = withValue,
                        Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = valid ? [] : [Issue(withId, "invalid_replacement", "Choose a different Warlock spell of the same arcanum level.")],
                        Details = new() { Replacement = true, ReplacementPath = ["class_options", selectedPool.Key], SpellLevel = selectedPool.Level } });
                }
                result.Add(new(choices.AsReadOnly(), selectedPool is null ? [] : ["class_options", selectedPool.Key], selectedPool is not null, selectedPool?.Existing[0] ?? "", with));
            }
        }
        var sourceDescriptions = Describe(source).Definitions;
        foreach (var list in new[] { "Cleric", "Druid", "Wizard" })
        {
            var cantrips = sourceDescriptions.SingleOrDefault(d => d.Id == "origin_" + list + "_cantrips");
            var leveled = sourceDescriptions.SingleOrDefault(d => d.Id == "origin_" + list + "_spell");
            if (cantrips is null || leveled is null) continue;
            var oldCantrips = Names(At(source.Build, cantrips.Path)); var oldSpell = Text(At(source.Build, leveled.Path));
            if (oldCantrips.Length != 2 || oldSpell.Length == 0) continue;
            var prefix = draft.ClassName.ToLowerInvariant() + "_" + draft.TargetLevel + "_magic_initiate_" + list.ToLowerInvariant(); var controlId = prefix + "_replacement"; var withId = prefix + "_replace_with";
            draft.Selections.TryGetValue(controlId, out var controlValue); var from = controlValue is null ? "keep" : Text(controlValue);
            var old = oldCantrips.Append(oldSpell).ToArray(); var controlValid = from == "keep" || old.Contains(from, StringComparer.Ordinal);
            var choices = new List<BuilderChoiceDefinition>
            {
                new() { Id = controlId, Label = "Change one Magic Initiate (" + list + ") spell?", Section = "spells", Mode = BuildChoiceMode.Single, Required = false,
                    Options = new[] { Named("keep", "Keep this feat's current spells") }.Concat(old.Select(s => Named(s, "Replace " + s))).ToArray(),
                    PreservedSelection = ChoiceValue.Of(from), Status = controlValid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved,
                    Issues = controlValid ? [] : [Issue(controlId, "invalid_replacement", "Choose one spell owned by this Magic Initiate source or keep its current spells.")] }
            };
            draft.Selections.TryGetValue(withId, out var selected); var with = Text(selected); var replace = controlValid && from != "keep"; var scalar = from == oldSpell;
            var definition = scalar ? leveled : cantrips; var actual = descriptions.SingleOrDefault(d => d.Path.SequenceEqual(definition.Path));
            if (replace)
            {
                var excluded = scalar ? new[] { oldSpell } : oldCantrips;
                var options = (actual?.Options ?? definition.Options).Where(o => !o.Disabled && !excluded.Contains(Text(o.Value), StringComparer.Ordinal)).ToArray();
                var valid = options.Any(o => Text(o.Value) == with);
                choices.Add(new() { Id = withId, Label = "Replacement " + (scalar ? "level 1 spell" : "cantrip") + " from " + list, Section = "spells", Mode = BuildChoiceMode.Single, Options = options, PreservedSelection = selected,
                    Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = valid ? [] : [Issue(withId, "invalid_replacement", "Choose a different spell of the same level from this feat's spell list.")],
                    Details = new() { Replacement = true, ReplacementPath = definition.Path, SpellList = list, SpellLevel = scalar ? 1 : 0 } });
            }
            result.Add(new(choices.AsReadOnly(), definition.Path.ToArray(), replace, from, with, scalar));
        }
        return result;
    }

    private IReadOnlyList<RitualBinding> RitualChoices(DraftCharacter source, DraftCharacter candidate, LevelUpDraft draft)
    {
        var result = new List<RitualBinding>(); var targetCount = CharacterCalculations.ProficiencyBonus(draft.TargetLevel);
        var owned = (source.Build.Feats ?? []).Where(f => f.Name == "Ritual Caster").ToArray();
        foreach (var feat in owned)
        {
            var existing = ChoiceReading.Names(feat.Choices.Get("spells")).Distinct(StringComparer.Ordinal).ToArray(); var needed = targetCount - existing.Length;
            if (needed <= 0) continue;
            var instance = feat.InstanceId; var featId = feat.FeatId;
            if (featId.Length == 0) featId = featRecords.SingleOrDefault(f => f.Name == "Ritual Caster")?.Id ?? "";
            var id = "ritual_caster_" + draft.TargetLevel + "_ritual_spells" + (owned.Length > 1 ? "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instance)))[..8].ToLowerInvariant() : "");
            var context = characters.FeatContext(candidate) with { EditingSourceId = "feat:" + instance };
            var field = feats.DescribeChoices(featId, context).SingleOrDefault(f => f.Key == "spells");
            var options = (field?.Options ?? []).Except(existing, StringComparer.Ordinal).ToArray();
            var value = draft.Selections.TryGetValue(id, out var selected) ? selected : ChoiceValue.ListOf([]);
            var valid = value.Items is { } a && a.Count == needed && a.All(n => n.Text is { } s && options.Contains(s, StringComparer.Ordinal)) && Names(value).Distinct(StringComparer.Ordinal).Count() == needed;
            var issues = valid ? System.Array.Empty<BuildIssue>() : new[] { Issue(id, "missing_selection", "Choose " + needed + " additional distinct level 1 Ritual Caster spell(s).") };
            result.Add(new(new() { Id = id, Label = "Additional Ritual Caster spells", Mode = BuildChoiceMode.Multi, Section = "spells", Choose = needed,
                Path = ["general_feats", instance, "choices", "spells"], Options = options.Select(s => Named(s, s)).ToArray(),
                PreservedSelection = value, Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = issues,
                Details = new() { FeatInstanceId = instance, SourceId = "feat:" + instance, ExistingValues = existing, TargetChoose = targetCount } }, instance, existing));
        }
        return result;
    }

    private IReadOnlyList<Binding> Bindings(DraftCharacter source, DraftCharacter candidate, LevelUpDraft draft, IReadOnlyDictionary<string, BuilderChoiceDefinition> sourceDefinitions)
    {
        var result = new List<Binding>();
        foreach (var original in Describe(candidate).Definitions)
        {
            var id = PublicId(original, draft); var path = original.Path.ToArray();
            if (original.Status == BuildChoiceStatus.Resolved && !draft.Selections.ContainsKey(id)) continue;
            sourceDefinitions.TryGetValue(PathKey(path), out var old);
            if (old is not null && original.Status == BuildChoiceStatus.Resolved && !draft.Selections.ContainsKey(id)) continue;
            var existing = original.Mode == BuildChoiceMode.Multi && old is not null ? Names(At(source.Build, path)).Distinct(StringComparer.Ordinal).ToArray() : [];
            var excluded = existing.Concat(original.Details.LockedValues).Distinct(StringComparer.Ordinal).ToArray();
            var needed = original.Mode == BuildChoiceMode.Multi ? Math.Max(0, original.Choose - excluded.Length) : original.Choose;
            if (original.Mode == BuildChoiceMode.Multi && needed == 0 && !draft.Selections.ContainsKey(id)) continue;
            var options = original.Options.Where(o => !excluded.Contains(Text(o.Value), StringComparer.Ordinal)).ToArray();
            ChoiceValue? value = draft.Selections.TryGetValue(id, out var selected) ? selected : existing.Length > 0 ? ChoiceValue.ListOf([]) : original.PreservedSelection;
            var allowed = options.Where(o => !o.Disabled).Select(o => Text(o.Value)).ToHashSet(StringComparer.Ordinal);
            var valid = original.Mode == BuildChoiceMode.Multi
                ? value?.Items is { } array && array.Count == needed && array.All(v => v.Text is { } s && allowed.Contains(s)) && Names(value).Distinct(StringComparer.Ordinal).Count() == array.Count
                : Text(value).Length > 0 && allowed.Contains(Text(value));
            var ownIssues = valid ? System.Array.Empty<BuildIssue>() : new[] { Issue(id, "missing_selection", "Choose the required distinct eligible options for " + original.Label + ".") };
            var details = original.Details with { ExistingValues = existing, TargetChoose = original.Choose, EffectiveTotalSelection = original.Details.EffectiveSelection ?? original.Details.EffectiveTotalSelection, EffectiveSelection = value };
            result.Add(new(original with { Id = id, Choose = needed, Options = options, PreservedSelection = value, Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Details = details, Issues = ownIssues }, existing, path));
        }
        if (result.Select(b => b.Definition.Id).Distinct(StringComparer.Ordinal).Count() != result.Count) throw new LevelUpException("duplicate_choice", "Two active level-up choices have the same identity.");
        return result;
    }

    private DraftCharacter ApplyBinding(DraftCharacter candidate, Binding binding, ChoiceValue selected)
    {
        if (binding.RawPath.Length == 0) return candidate;
        var value = binding.Definition.Mode == BuildChoiceMode.Multi && binding.Existing.Length > 0 && selected.Items is { } added
            ? ChoiceValue.ListOf(binding.Existing.Select(ChoiceValue.Of).Concat(added)) : selected;
        return ApplyPath(candidate, binding.RawPath, value);
    }

    private DraftCharacter ApplyPath(DraftCharacter candidate, string[] path, ChoiceValue value)
        => With(candidate, edits.Apply(candidate.Build, ChoiceValue.Record([]).With(path, value)));

    private static string PublicId(BuilderChoiceDefinition definition, LevelUpDraft draft)
    {
        var prefix = draft.ClassName.ToLowerInvariant() + "_" + draft.TargetLevel + "_";
        if (definition.Path.SequenceEqual(new[] { "subclass" })) return prefix + draft.ClassName.ToLowerInvariant() + "_subclass";
        return definition.Id switch { "spells_cantrips" => prefix + "cantrips", "spells_prepared" => prefix + "prepared_spells", "spells_known" => prefix + "known_spells", "spells_spellbook" => prefix + "spellbook_spells", "weapon_masteries" => prefix + "weapon_mastery", _ => prefix + definition.Id };
    }

    private BuilderChoiceDefinition ImprovementDefinition(DraftCharacter candidate, LevelUpDraft draft, ProgressionFeature gain)
    {
        var id = gain.Id; draft.Selections.TryGetValue(id, out var selected);
        var issues = new List<BuildIssue>();
        // Eligibility is assessed before the proposed feat/ASI, never against its own grants.
        var probe = Settle(With(candidate, RemoveImprovement(candidate.Build, draft, id)));
        var context = characters.FeatContext(probe);
        if (selected is null) issues.Add(Issue(id, "missing_selection", "Choose an ability score improvement or an eligible feat."));
        else ApplyImprovement(probe, draft, id, selected, issues);
        var candidates = new List<BuildChoiceOption>();
        foreach (var feat in featRecords.Where(f => f.Name != "Ability Score Improvement"))
        {
            var featId = feat.Id; var eligibility = feats.Evaluate(featId, context);
            candidates.Add(new()
            {
                Value = ChoiceValue.Of(featId), Description = feat.Description, Disabled = !eligibility.Selectable,
                Label = feat.Name + (eligibility.Selectable ? "" : " · " + string.Join(" ", eligibility.Issues.Select(i => i.Message))),
                Children = FeatChildren(feat, context, selected is { Fields: not null } current && Text(current.At(["feat_id"])) == featId ? current : ChoiceValue.Record([]))
            });
        }
        return new() { Id = id, Label = gain.Name, Mode = BuildChoiceMode.AsiOrFeat, Path = [], Section = "abilities", PreservedSelection = selected,
            Status = issues.Count == 0 ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = issues.AsReadOnly(), Details = new() { FeatOptions = candidates, Abilities = Abilities } };
    }

    private IReadOnlyList<BuilderChoiceDefinition> FeatChildren(CatalogFeat feat, FeatRuleContext context, ChoiceValue selected)
    {
        var output = new List<BuilderChoiceDefinition>(); var name = feat.Name;
        void Add(string key, string label, string[] path, IEnumerable<string> options, int choose, bool multi)
        {
            var allowed = options.Distinct(StringComparer.Ordinal).ToArray(); var value = selected.At(path) is { IsNull: false } chosen ? chosen : multi ? ChoiceValue.ListOf([]) : ChoiceValue.Of("");
            var valid = multi ? value.Items is { } array && array.Count == choose && Names(value).Distinct(StringComparer.Ordinal).Count() == choose && Names(value).All(allowed.Contains)
                : allowed.Contains(Text(value), StringComparer.Ordinal);
            output.Add(new() { Id = key, Label = label, Mode = multi ? BuildChoiceMode.Multi : BuildChoiceMode.Single, Section = "feat", Path = path, Choose = choose, Required = true,
                PreservedSelection = value, Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Options = allowed.Select(s => new BuildChoiceOption { Value = ChoiceValue.Of(s), Label = s, Source = name }).ToArray() });
        }
        if (feat.AbilityIncrease is { Mode: "choose" } ability)
        {
            var options = ability.From.AsEnumerable();
            if (name == "Resilient") options = options.Except(context.ProficiencySources.Where(s => s.Kind == ProficiencySourceKind.Save && s.SourceId != context.EditingSourceId).Select(s => s.Target), StringComparer.Ordinal);
            Add("feat_ability_choice", "Feat ability increase", ["feat_ability_choice"], options, 1, false);
        }
        var fields = feats.DescribeChoices(feat.Id, context);
        foreach (var field in fields)
        {
            IEnumerable<string> options = field.Options;
            if (name == "Skill Expert" && field.Key == "expertise")
            {
                var proposed = Text(selected.At(["feat_choices", "skill_proficiency"]));
                if (fields.Single(f => f.Key == "skill_proficiency").Options.Contains(proposed, StringComparer.Ordinal)) options = options.Append(proposed);
            }
            Add(field.Key, field.Label, ["feat_choices", field.Key], options, field.Choose, field.Mode == BuildChoiceMode.Multi);
        }
        return output;
    }

    private static string InstanceId(LevelUpDraft draft, string id) => draft.TransactionId + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..12].ToLowerInvariant();
    private static BuildFacts RemoveImprovement(BuildFacts c, LevelUpDraft draft, string id)
    {
        var instance = InstanceId(draft, id);
        return c with
        {
            Feats = c.Feats is { } list ? [.. list.Where(f => f.InstanceId != instance)] : null,
            AbilityScoreAdjustments = c.AbilityScoreAdjustments is { } adjustments ? [.. adjustments.Where(a => a.FeatInstanceId != instance && !(a.LevelUpChoiceId == id && a.Source?.TransactionId == draft.TransactionId))] : null
        };
    }

    private DraftCharacter ApplyImprovement(DraftCharacter character, LevelUpDraft draft, string id, ChoiceValue value, List<BuildIssue> issues)
    {
        if (value.Fields is null) { issues.Add(Issue(id, "invalid_selection", "Choose a structured ability improvement or feat.")); return character; }
        var c = character.Build; var mode = Text(value.At(["mode"])); var context = characters.FeatContext(character); Dictionary<string, int> allocation; CatalogFeat? chosenFeat = null;
        Dictionary<string, int> Supplied() => (value.At(["ability_increases"])?.Fields ?? new Dictionary<string, ChoiceValue>()).ToDictionary(p => p.Key, p => p.Value.Number ?? -1);
        if (mode == "asi")
        {
            allocation = Supplied(); var amounts = allocation.Values.Order().ToArray();
            if (!(amounts.SequenceEqual(new[] { 2 }) || amounts.SequenceEqual(new[] { 1, 1 })) || allocation.Any(p => !Abilities.Contains(p.Key) || context.Scores.GetValueOrDefault(p.Key) + p.Value > 20))
            { issues.Add(Issue(id, "invalid_asi", "Choose +2 to one ability or +1 to two abilities, within the ability cap.")); return character; }
        }
        else if (mode == "feat")
        {
            var featId = Text(value.At(["feat_id"])); chosenFeat = featRecords.FirstOrDefault(f => f.Id == featId);
            if (chosenFeat is null) { issues.Add(Issue(id, "unknown_feat", "Choose a known eligible feat.")); return character; }
            var ability = Text(value.At(["feat_ability_choice"])); var increase = chosenFeat.AbilityIncrease;
            if (increase?.Mode == "fixed") ability = increase.From.FirstOrDefault() ?? "";
            allocation = value.At(["ability_increases"])?.Fields is not null ? Supplied() : ability.Length > 0 ? new() { [ability] = increase?.Amount ?? 1 } : [];
            var verdict = feats.ValidateSelection(featId, context, value.At(["feat_choices"])?.Fields ?? new Dictionary<string, ChoiceValue>(), allocation);
            if (!verdict.Valid) { issues.AddRange(verdict.Issues); return character; }
        }
        else { issues.Add(Issue(id, "invalid_selection", "Choose an ability improvement or feat.")); return character; }
        c = RemoveImprovement(c, draft, id);
        var source = new BuildSourceFact { Type = "class_level", ClassName = draft.ClassName, Level = draft.TargetLevel, TransactionId = draft.TransactionId };
        var adjustment = new AbilityScoreAdjustmentFact { Type = "ability_score_adjustment", SourceType = chosenFeat is null ? "class_level" : "feat", SourceClass = draft.ClassName,
            SourceLevel = draft.TargetLevel, Source = source, LevelUpChoiceId = id, Adjustments = allocation.ToDictionary(p => Enum.Parse<Ability>(p.Key), p => p.Value), Maximum = chosenFeat?.AbilityIncrease?.Maximum ?? 20 };
        if (chosenFeat is not null)
        {
            var instance = InstanceId(draft, id);
            var copied = new Dictionary<string, ChoiceValue>(value.At(["feat_choices"])?.Fields ?? new Dictionary<string, ChoiceValue>(), StringComparer.Ordinal);
            if (allocation.Count == 1) copied["ability"] = ChoiceValue.Of(allocation.First().Key);
            c = c with { Feats = [.. c.Feats ?? [], new FeatFact { InstanceId = instance, FeatId = chosenFeat.Id, Name = chosenFeat.Name, Repeatable = chosenFeat.Repeatable, Source = source, Choices = copied }] };
            adjustment = adjustment with { FeatInstanceId = instance, SourceFeat = chosenFeat.Id, SourceName = chosenFeat.Name };
        }
        if (allocation.Count > 0) c = c with { AbilityScoreAdjustments = [.. c.AbilityScoreAdjustments ?? [], adjustment] };
        return With(character, c);
    }

    /// <summary>Identifies the build a draft was started from, so a changed character makes the draft stale.</summary>
    public static string SourceHash(BuildFacts source, IReadOnlyList<EquipmentItem>? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(source); var count = 0;
        ChoiceValue Sort(ChoiceValue value, int depth)
        {
            if (depth > 64 || ++count > 250000) throw new LevelUpException("invalid_source", "The character exceeds supported structure bounds.");
            if (value.Fields is { } fields) return ChoiceValue.Record(fields.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Sort(p.Value, depth + 1))));
            if (value.Items is { } items) return ChoiceValue.ListOf(items.Select(item => Sort(item, depth + 1)));
            return value;
        }
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Sort(source.AsChoiceTree(), 0)) + JsonSerializer.Serialize(equipment ?? []));
        if (bytes.Length > 8 * 1024 * 1024) throw new LevelUpException("invalid_source", "The character exceeds its supported size.");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
