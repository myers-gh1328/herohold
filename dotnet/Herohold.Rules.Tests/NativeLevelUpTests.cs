using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Rules.Tests;

[TestClass]
public sealed class NativeLevelUpTests
{
    private static readonly Lazy<(RulesCatalogSnapshot Catalog, ClassProgressionCatalog Classes)> Catalogs = new(() =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "data/imported/5etools/classes.json"))) root = root.Parent;
        var path = Path.Combine(root!.FullName, "data/imported/5etools");string Read(string name) => File.ReadAllText(Path.Combine(path, name + ".json"));
        var classes = Read("classes");
        return (RulesCatalogSnapshot.Parse(classes, Read("species"), Read("backgrounds"), Read("feats"), Read("spells"), Read("equipment"), Read("weapons"), File.ReadAllText(Path.Combine(root.FullName, "data/raw/5etools/spells/spells-xphb.json"))), ClassProgressionCatalog.Parse(classes));
    });
    private static NativeLevelUpRules Native() => new(Catalogs.Value.Catalog, Catalogs.Value.Classes, new(Catalogs.Value.Catalog), new(Catalogs.Value.Catalog), new TestDraftRules());
    private static JsonObject Source(int level = 1)
    {
        var scores = new JsonObject();foreach (var ability in Enum.GetValues<Ability>()) scores[ability.ToString()] = ability == Ability.Constitution ? 14 : ability is Ability.Intelligence or Ability.Charisma ? 13 : 10;
        return new TestDraftRules().Recalculate(new JsonObject
        {
            ["character_id"] = "fictional-level-up", ["name"] = "Fictional Fighter", ["class"] = "Fighter", ["level"] = level,
            ["subclass"] = level >= 3 ? "Champion" : "", ["class_levels"] = new JsonObject { ["Fighter"] = level },
            ["base_ability_scores"] = scores, ["class_options"] = new JsonObject(), ["species_options"] = new JsonObject(), ["background_options"] = new JsonObject(),
            ["general_feats"] = new JsonArray(), ["ability_score_adjustments"] = new JsonArray(), ["hp_history"] = new JsonArray(),
            ["hit_points"] = new JsonObject { ["current"] = 5, ["temporary"] = 3 }, ["spells"] = new JsonObject(),
            ["folio_inventory"] = new JsonArray(new JsonObject { ["id"] = "sword", ["name"] = "Longsword", ["quantity"] = 1 }),
            ["folio_play_state"] = new JsonObject { ["spent"] = 1 }, ["unknown_safe_sidecar"] = new JsonObject { ["preserved"] = true }
        });
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void StartAndFixedPreviewAreDetachedAndLeaveTheSourceUnchanged()
    {
        var source = Source();var before = source.ToJsonString();var rules = Native();var review = rules.Start(source, 2);
        Assert.AreEqual(2, review.Build.Level);Assert.AreEqual(20, review.MaximumHp);Assert.AreEqual(8, review.HpIncrease);
        Assert.IsTrue(review.CanFinalize);CollectionAssert.Contains(review.AutomaticFeatures.ToArray(), "Action Surge");
        review.Build.ClassLevels!["Fighter"] = 9;Assert.AreEqual(2, rules.Preview(source, review.Draft).Build.ClassLevels!["Fighter"]);
        Assert.AreEqual(before, source.ToJsonString());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void PhysicalHitDieAndExplicitGainPolicyMatchPreviewAndCommitExactlyOnce()
    {
        var source = Source();var rules = Native();var draft = rules.Start(source, 2).Draft;
        var hp = new LevelUpHpOptions { Method = HitPointMethod.Rolled, Roll = 3, AddGainedHp = true };
        var preview = rules.Preview(source, draft, hp);var committed = rules.Commit(source, draft, hp);
        Assert.AreEqual(17, preview.MaximumHp);Assert.AreEqual(preview.MaximumHp, committed.MaximumHp);
        Assert.AreEqual(preview.Character["hp_history"]!.ToJsonString(), committed.Character["hp_history"]!.ToJsonString());Assert.IsTrue(committed.Draft.Completed);
        var entry = committed.Character["hp_history"]!.AsArray().Last()!;Assert.AreEqual("rolled", entry["method"]!.GetValue<string>());Assert.AreEqual(3, entry["base_gain"]!.GetValue<int>());
        Assert.AreEqual(1, committed.Character["completed_level_up_transactions"]!.AsArray().Count);
        Assert.AreEqual("already_completed", Assert.Throws<LevelUpException>(() => rules.Commit(committed.Character, committed.Draft, hp)).Code);
        Assert.AreEqual(1, source["level"]!.GetValue<int>());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void SourceHashIgnoresKeyOrderAndDetectsBuildAndInventoryChangesButNotHealthInPlay()
    {
        var source = Source();var reverse = new JsonObject();foreach (var pair in source.Reverse()) reverse[pair.Key] = pair.Value?.DeepClone();
        Assert.AreEqual(NativeLevelUpRules.SourceHash(BuildFacts.FromCanonical(source)), NativeLevelUpRules.SourceHash(BuildFacts.FromCanonical(reverse)));
        var rules = Native();var draft = rules.Start(source, 2).Draft;
        foreach (var field in new[] { "name", "folio_inventory" })
        {
            var changed = source.DeepClone().AsObject();
            if (field == "name") changed[field] = "Renamed";
            else changed[field]![0]!["quantity"] = 2;
            Assert.AreEqual("stale_draft", Assert.Throws<LevelUpException>(() => rules.Preview(changed, draft)).Code);
        }
        var healed = source.DeepClone().AsObject();healed["hit_points"]!["current"] = 4;
        Assert.IsNotNull(rules.Preview(healed, draft), "HP in play is not part of the build a level-up advances.");
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void SubclassSelectionRefreshesDependentChoicesAndInactiveSelectionsDoNotGrantAnything()
    {
        var source = Source(2);var rules = Native();var draft = rules.Start(source, 3);
        Assert.IsFalse(draft.CanFinalize);var subclass = draft.PendingChoices.Single(c => c.Path.SequenceEqual(new[] { "subclass" }));
        var battle = rules.Choose(source, draft.Draft, subclass.Id, JsonValue.Create("Battle Master"));
        var child = battle.PendingChoices.Single(c => c.Path.SequenceEqual(new[] { "class_options", "student_of_war_skill" }));Assert.IsFalse(battle.CanFinalize);
        var selected = rules.Choose(source, battle.Draft, child.Id, JsonValue.Create("Insight"));Assert.IsTrue(selected.CanFinalize);
        var switched = rules.Choose(source, selected.Draft, subclass.Id, JsonValue.Create("Champion"));Assert.IsTrue(switched.CanFinalize);
        Assert.IsFalse(switched.PendingChoices.Any(c => c.Path.LastOrDefault() == "student_of_war_skill"));
        Assert.IsNull(switched.Character["class_options"]!["student_of_war_skill"]);
        Assert.AreEqual("invalid_choice", Assert.Throws<LevelUpException>(() => rules.Choose(source, switched.Draft, "../../name", JsonValue.Create("unsafe"))).Code);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void ConstitutionAsiRetroactivelyChangesMaximum()
    {
        var source = Source(3);var rules = Native();var initial = rules.Start(source, 4);var asi = initial.PendingChoices.Single(c => c.Mode == BuildChoiceMode.AsiOrFeat);
        Assert.IsFalse(initial.CanFinalize);
        var chosen = rules.Choose(source, initial.Draft, asi.Id, JsonNode.Parse("""{"mode":"asi","ability_increases":{"Constitution":2}}"""));
        Assert.IsTrue(chosen.CanFinalize);Assert.AreEqual(16, new TestDraftRules().Recalculate(chosen.Character)["ability_scores"]!["Constitution"]!.GetValue<int>());
        Assert.AreEqual(40, chosen.MaximumHp);Assert.AreEqual(12, chosen.HpIncrease);
        var committed = rules.Commit(source, chosen.Draft, new() { AddGainedHp = true });Assert.AreEqual(40, committed.MaximumHp);
        Assert.AreEqual(1, committed.Character["ability_score_adjustments"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void FeatSelectionCreatesOneStableSourceAndDoesNotStackDuringRepeatedPreview()
    {
        var source = Source(3);var rules = Native();var start = rules.Start(source, 4);var choice = start.PendingChoices.Single(c => c.Mode == BuildChoiceMode.AsiOrFeat);
        var selected = rules.Choose(source, start.Draft, choice.Id, JsonNode.Parse("""{"mode":"feat","feat_id":"actor_xphb","feat_ability_choice":"Charisma","feat_choices":{}}"""));
        Assert.IsTrue(selected.CanFinalize);
        var first = rules.Preview(source, selected.Draft);var second = rules.Preview(source, selected.Draft);
        Assert.AreEqual(1, first.Character["general_feats"]!.AsArray().Count);Assert.AreEqual(1, first.Character["ability_score_adjustments"]!.AsArray().Count);
        Assert.AreEqual(first.Character.ToJsonString(), second.Character.ToJsonString());Assert.AreEqual(14, new TestDraftRules().Recalculate(first.Character)["ability_scores"]!["Charisma"]!.GetValue<int>());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void PartialSkillExpertExposesStructuredDependentChildrenWithoutGrantingAnIncompleteFeat()
    {
        var source = Source(3);var rules = Native();var start = rules.Start(source, 4);var asi = start.PendingChoices.Single(c => c.Mode == BuildChoiceMode.AsiOrFeat);
        var partialValue = JsonNode.Parse("""{"mode":"feat","feat_id":"skill_expert_xphb","feat_ability_choice":"Wisdom","feat_choices":{"skill_proficiency":"Insight"}}""")!;
        var partial = rules.Choose(source, start.Draft, asi.Id, partialValue);Assert.IsFalse(partial.CanFinalize);Assert.AreEqual(0, partial.Character["general_feats"]!.AsArray().Count);
        var descriptor = partial.PendingChoices.Single(c => c.Id == asi.Id);
        var option = descriptor.Details.FeatOptions.Single(n => n.Value.Text == "skill_expert_xphb");
        Assert.IsFalse(option.Disabled);
        var children = option.Children;Assert.AreEqual(3, children.Count);
        var expertise = children.Single(n => n.Id == "expertise");
        CollectionAssert.AreEqual(new[] { "feat_choices", "expertise" }, expertise.Path.ToArray());
        Assert.IsTrue(expertise.Options.Any(n => n.Value.Text == "Insight"));
        partialValue["feat_choices"]!["expertise"] = "Insight";
        var completed = rules.Choose(source, partial.Draft, asi.Id, partialValue);Assert.IsTrue(completed.CanFinalize);
        Assert.AreEqual(1, completed.Character["general_feats"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void InvalidTargetsDiceAndUnresolvedDraftsCannotCommit()
    {
        var rules = Native();var source = Source();
        Assert.Throws<LevelUpException>(() => rules.Start(source, 3));Assert.Throws<LevelUpException>(() => rules.Start(Source(20), 21));
        var draft = rules.Start(source, 2).Draft;
        foreach (var roll in new[] { 0, 11 }) Assert.Throws<LevelUpException>(() => rules.Preview(source, draft, new() { Method = HitPointMethod.Rolled, Roll = roll }));
        Assert.Throws<LevelUpException>(() => rules.Preview(source, draft, new() { Method = HitPointMethod.Maximum }));
        var pending = rules.Start(Source(3), 4);Assert.AreEqual("incomplete_levelup", Assert.Throws<LevelUpException>(() => rules.Commit(Source(3), pending.Draft)).Code);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void ACallerConstructedDraftCannotBypassValidationOfTheCurrentSourceCharacter()
    {
        var source = Source(3);source["subclass"] = "";
        var draft = new LevelUpDraft { TransactionId = Guid.NewGuid().ToString("N"), SourceHash = NativeLevelUpRules.SourceHash(BuildFacts.FromCanonical(source), CanonicalFixtures.Draft(source).Equipment), ClassName = "Fighter", CurrentLevel = 3, TargetLevel = 4 };
        Assert.AreEqual("incomplete_character", Assert.Throws<LevelUpException>(() => Native().Preview(source, draft)).Code);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void HistoricalWarningsRemainVisibleWithoutBlockingAnOtherwiseValidLevelUp()
    {
        var source = Source();source["class_options"]!["fixture_warning"] = true;var rules = Native();var review = rules.Start(source, 2);
        Assert.IsTrue(review.CanFinalize);Assert.IsTrue(review.Issues.Any(i => i.Severity == BuildIssueSeverity.Warning));
        Assert.IsTrue(rules.Commit(source, review.Draft).Draft.Completed);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExistingRitualCasterGainsOnlyTheNewPbSpellAndKeepsItsOriginalSource(bool legacyMissingId)
    {
        var source = Source(4);
        source["general_feats"] = new JsonArray(new JsonObject
        {
            ["instance_id"] = "ritual-kept", ["feat_id"] = "ritual_caster_xphb", ["name"] = "Ritual Caster",
            ["source"] = new JsonObject { ["type"] = "class_level", ["class_name"] = "Fighter", ["level"] = 4 },
            ["choices"] = new JsonObject { ["ability"] = "Intelligence", ["spells"] = new JsonArray("Alarm", "Identify") }
        });
        if (legacyMissingId) source["general_feats"]![0]!["feat_id"] = null;
        var before = source.ToJsonString();var rules = Native();var start = rules.Start(source, 5);
        var ritual = start.PendingChoices.Single(c => c.Id == "ritual_caster_5_ritual_spells");
        Assert.AreEqual(1, ritual.Choose);Assert.IsFalse(start.CanFinalize);
        Assert.IsFalse(ritual.Options.Any(o => o.Value.Text is "Alarm" or "Identify"));
        var invalid = rules.Choose(source, start.Draft, ritual.Id, new JsonArray("Alarm"));Assert.IsFalse(invalid.CanFinalize);
        var repaired = rules.Choose(source, invalid.Draft, ritual.Id, new JsonArray("Detect Magic"));Assert.IsTrue(repaired.CanFinalize);
        var committed = rules.Commit(source, repaired.Draft);var feats = committed.Character["general_feats"]!.AsArray();Assert.AreEqual(1, feats.Count);
        Assert.AreEqual("ritual-kept", feats[0]!["instance_id"]!.GetValue<string>());
        CollectionAssert.AreEqual(new[] { "Alarm", "Identify", "Detect Magic" }, feats[0]!["choices"]!["spells"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.AreEqual(0, committed.Character["ability_score_adjustments"]!.AsArray().Count);Assert.AreEqual(before, source.ToJsonString());
    }

    private static JsonObject Caster(string name)
    {
        var c = Source();c["class"] = name;c["class_levels"] = new JsonObject { [name] = 1 };
        c["spells"] = name switch
        {
            "Bard" => new JsonObject { ["cantrips"] = new JsonArray("Mage Hand", "Prestidigitation"), ["prepared"] = new JsonArray("Charm Person", "Cure Wounds", "Healing Word", "Thunderwave") },
            "Cleric" => new JsonObject { ["cantrips"] = new JsonArray("Guidance", "Sacred Flame", "Thaumaturgy"), ["prepared"] = new JsonArray("Bless", "Cure Wounds", "Healing Word", "Shield of Faith") },
            _ => new JsonObject { ["cantrips"] = new JsonArray("Fire Bolt", "Light", "Mage Hand"), ["prepared"] = new JsonArray(), ["spellbook"] = new JsonArray("Detect Magic", "Find Familiar", "Mage Armor", "Magic Missile", "Shield", "Sleep") }
        };
        return new TestDraftRules().Recalculate(c);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void NewPreparedQuotaAppendsExistingSpellsAndReplacementRequiresExplicitOptIn()
    {
        var source = Caster("Bard");var rules = Native();var start = rules.Start(source, 2);
        var addition = start.PendingChoices.Single(c => c.Id == "bard_2_prepared_spells");Assert.AreEqual(1, addition.Choose);
        Assert.IsFalse(addition.Options.Any(o => o.Value.Text == "Charm Person"));
        var chosen = rules.Choose(source, start.Draft, addition.Id, new JsonArray("Sleep"));Assert.IsTrue(chosen.CanFinalize);
        CollectionAssert.AreEqual(new[] { "Charm Person", "Cure Wounds", "Healing Word", "Thunderwave", "Sleep" }, chosen.Character["spells"]!["prepared"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        var option = chosen.PendingChoices.Single(c => c.Id == "bard_2_prepared_replacement");Assert.IsFalse(option.Required);Assert.AreEqual("keep", option.PreservedSelection!.Text);
        var replace = rules.Choose(source, chosen.Draft, option.Id, JsonValue.Create("replace"));Assert.IsFalse(replace.CanFinalize);
        replace = rules.Choose(source, replace.Draft, "bard_2_prepared_replace_from", JsonValue.Create("Charm Person"));
        replace = rules.Choose(source, replace.Draft, "bard_2_prepared_replace_with", JsonValue.Create("Dissonant Whispers"));Assert.IsTrue(replace.CanFinalize);
        var final = replace.Character["spells"]!["prepared"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        CollectionAssert.DoesNotContain(final, "Charm Person");CollectionAssert.Contains(final, "Dissonant Whispers");Assert.AreEqual(5, final.Length);
        CollectionAssert.Contains(source["spells"]!["prepared"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray(), "Charm Person");
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void WizardBookOnlyAddsTwoAndWizardCantripReplacementIsReservedForLongRest()
    {
        var source = Caster("Wizard");var rules = Native();var start = rules.Start(source, 2);
        var book = start.PendingChoices.Single(c => c.Id == "wizard_2_spellbook_spells");Assert.AreEqual(2, book.Choose);
        Assert.IsFalse(start.PendingChoices.Any(c => c.Id == "wizard_2_cantrips_replacement"));
        var chosen = rules.Choose(source, start.Draft, book.Id, new JsonArray("Burning Hands", "Feather Fall"));Assert.IsTrue(chosen.CanFinalize);
        var union = chosen.Character["spells"]!["spellbook"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();Assert.AreEqual(8, union.Length);
        foreach (var spell in source["spells"]!["spellbook"]!.AsArray()) CollectionAssert.Contains(union, spell!.GetValue<string>());
        Assert.AreEqual(0, chosen.Character["spells"]!["prepared"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void ClericLevelUpIsNotAFullPreparedSpellChangeButCanExplicitlyReplaceOneCantrip()
    {
        var source = Caster("Cleric");var rules = Native();var start = rules.Start(source, 2);
        Assert.IsFalse(start.PendingChoices.Any(c => c.Id == "cleric_2_prepared_replacement"));
        var addition = start.PendingChoices.Single(c => c.Id == "cleric_2_prepared_spells");Assert.AreEqual(1, addition.Choose);
        var selected = rules.Choose(source, start.Draft, addition.Id, new JsonArray("Sanctuary"));Assert.IsTrue(selected.CanFinalize);
        Assert.IsTrue(selected.PendingChoices.Any(c => c.Id == "cleric_2_cantrips_replacement"));
        var before = source["spells"]!.ToJsonString();
        var invalid = rules.Choose(source, selected.Draft, addition.Id, new JsonArray("Bless"));Assert.IsFalse(invalid.CanFinalize);
        Assert.AreEqual(before, source["spells"]!.ToJsonString());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void WarlockCanExplicitlyReplaceOneArcanumWithASpellOfTheSameLevelOnly()
    {
        var source = Source(11);source["class"] = "Warlock";source["subclass"] = "";source["class_levels"] = new JsonObject { ["Warlock"] = 11 };
        var spells = Catalogs.Value.Catalog.SpellRecords.Where(s => s.Classes.Contains("Warlock")).ToArray();
        source["spells"] = new JsonObject
        {
            ["cantrips"] = new JsonArray(spells.Where(s => s.Level == 0).Take(4).Select(s => (JsonNode?)s.Name).ToArray()),
            ["prepared"] = new JsonArray(spells.Where(s => s.Level is >= 1 and <= 5).Take(11).Select(s => (JsonNode?)s.Name).ToArray())
        };
        source["class_options"] = new JsonObject { ["mystic_arcanum_6"] = new JsonArray("Eyebite") };source = new TestDraftRules().Recalculate(source);
        var rules = Native();var review = rules.Start(source, 12);var asi = review.PendingChoices.Single(c => c.Mode == BuildChoiceMode.AsiOrFeat);
        review = rules.Choose(source, review.Draft, asi.Id, JsonNode.Parse("""{"mode":"asi","ability_increases":{"Strength":2}}"""));
        var control = review.PendingChoices.Single(c => c.Id == "warlock_12_mystic_arcanum_replacement");Assert.AreEqual("keep", control.PreservedSelection!.Text);
        review = rules.Choose(source, review.Draft, control.Id, JsonValue.Create("mystic_arcanum_6"));Assert.IsFalse(review.CanFinalize);
        var replacement = review.PendingChoices.Single(c => c.Id == "warlock_12_mystic_arcanum_replace_with");
        Assert.IsTrue(replacement.Options.Any(o => o.Value.Text == "Circle of Death"));Assert.IsFalse(replacement.Options.Any(o => o.Value.Text == "Forcecage"));
        var invalid = rules.Choose(source, review.Draft, replacement.Id, JsonValue.Create("Forcecage"));Assert.IsFalse(invalid.CanFinalize);
        var valid = rules.Choose(source, invalid.Draft, replacement.Id, JsonValue.Create("Circle of Death"));Assert.IsTrue(valid.CanFinalize);
        Assert.AreEqual("Circle of Death", valid.Character["class_options"]!["mystic_arcanum_6"]![0]!.GetValue<string>());
        Assert.AreEqual("Eyebite", source["class_options"]!["mystic_arcanum_6"]![0]!.GetValue<string>());
    }

    private sealed class TestDraftRules : ICharacterDraftRules
    {
        private static JsonObject Document(DraftCharacter character) => character.Build.ToCanonical("fixture");
        public SettledBuild Recalculate(DraftCharacter character) { var c = Recalculate(Document(character)); return new(BuildFacts.FromCanonical(c), c["hit_points"]!["maximum"]!.GetValue<int>()); }
        public CreationChoiceReport Describe(DraftCharacter character) => Describe(Document(character));
        public FeatRuleContext FeatContext(DraftCharacter character) => FeatContext(Recalculate(Document(character)));
        internal JsonObject Recalculate(JsonObject character)
        {
            var c = character.DeepClone().AsObject();
            var adjustments = (c["ability_score_adjustments"] as JsonArray ?? []).OfType<JsonObject>().Select((entry, index) => new ScoreAdjustment
            {
                SourceId = "fixture:" + index, SourceName = "Fixture adjustment", Maximum = entry["maximum"]?.GetValue<int>() ?? 20,
                Increases = (entry["adjustments"] as JsonObject ?? new()).ToDictionary(p => Enum.Parse<Ability>(p.Key), p => p.Value!.GetValue<int>())
            }).ToArray();
            var className = c["class"]!.GetValue<string>();
            var scores = AbilityScoreRules.Calculate(new() { ClassName = className, Level = c["level"]!.GetValue<int>(), BaseScores = c["base_ability_scores"]!.AsObject().ToDictionary(p => Enum.Parse<Ability>(p.Key), p => p.Value!.GetValue<int>()), Adjustments = adjustments }).Scores;
            c["ability_scores"] = new JsonObject(scores.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key.ToString(), JsonValue.Create(p.Value))));
            var die = Catalogs.Value.Classes.Classes.Single(x => x.Name == className).HitDie;
            var hp = HitPointRules.Calculate(CanonicalFixtures.HitPointsOfBuild(c, die, CharacterCalculations.AbilityModifier(scores[Ability.Constitution])));
            c["hit_points"] = new JsonObject { ["maximum"] = hp.Maximum, ["max"] = hp.Maximum, ["current"] = hp.CurrentHp, ["temporary"] = hp.TemporaryHp };
            return c;
        }
        private CreationChoiceReport Describe(JsonObject c)
        {
            var choices = new List<BuilderChoiceDefinition>();var level = c["level"]!.GetValue<int>();var subclass = c["subclass"]?.GetValue<string>() ?? "";
            void Add(string id, string[] path, string[] options)
            {
                JsonNode? selected = c;foreach (var part in path) selected = selected?[part];
                var valid = options.Contains(selected?.GetValue<string>() ?? "");var issues = valid ? Array.Empty<BuildIssue>() : new[] { new BuildIssue("missing_selection", path, "Select " + id) };
                choices.Add(new() { Id = id, Path = path, Mode = BuildChoiceMode.Single, Choose = 1, Options = options.Select(s => new BuildChoiceOption { Value = ChoiceValue.Of(s), Label = s }).ToArray(), PreservedSelection = CanonicalFixtures.Choice(selected), Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = issues });
            }
            var className = c["class"]!.GetValue<string>();
            if (className == "Fighter" && level >= 3) Add("subclass", ["subclass"], ["Champion", "Battle Master"]);
            if (className == "Fighter" && level >= 3 && subclass == "Battle Master") Add("student_of_war_skill", ["class_options", "student_of_war_skill"], ["Insight"]);
            var casting = ClassProgressionRules.AtLevel(Catalogs.Value.Classes, className, level).Spellcasting;
            if (casting is not null)
                foreach (var pair in new[] { ("cantrips", casting.CantripLimit), ("prepared", casting.PreparedLimit), ("spellbook", casting.SpellbookLimit) })
                {
                    if (pair.Item2 == 0) continue;
                    var field = pair.Item1;var selected = (c["spells"]?[field] as JsonArray ?? []).Select(n => n!.GetValue<string>()).ToArray();
                    var options = Catalogs.Value.Catalog.SpellRecords.Where(s => s.Classes.Contains(casting.SpellList)
                        && (field == "cantrips" ? s.Level == 0 : s.Level is >= 1 && s.Level <= casting.AvailableSpellLevels.DefaultIfEmpty(1).Max())).Select(s => s.Name).ToArray();
                    var required = !(field == "prepared" && className == "Wizard");var valid = (required ? selected.Length == pair.Item2 : selected.Length <= pair.Item2) && selected.Distinct().Count() == selected.Length && selected.All(options.Contains);
                    var path = new[] { "spells", field };var ownIssues = valid ? System.Array.Empty<BuildIssue>() : new[] { new BuildIssue("invalid_choice", path, "Choose spells") };
                    choices.Add(new() { Id = "spells_" + field, Path = path, Mode = BuildChoiceMode.Multi, Choose = pair.Item2, Required = required, Options = options.Select(s => new BuildChoiceOption { Value = ChoiceValue.Of(s), Label = s }).ToArray(), PreservedSelection = ChoiceValue.ListOf(selected.Select(ChoiceValue.Of)), Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = ownIssues });
                }
            if (className == "Warlock")
                foreach (var (gate, spellLevel) in new[] { (11, 6), (13, 7), (15, 8), (17, 9) })
                {
                    if (level < gate) continue;
                    var key = "mystic_arcanum_" + spellLevel;var options = Catalogs.Value.Catalog.SpellRecords.Where(s => s.Level == spellLevel && s.Classes.Contains("Warlock")).Select(s => s.Name).ToArray();
                    var selected = c["class_options"]?[key] as JsonArray ?? new();var valid = selected.Count == 1 && options.Contains(selected[0]!.GetValue<string>());
                    var path = new[] { "class_options", key };var own = valid ? System.Array.Empty<BuildIssue>() : new[] { new BuildIssue("invalid_choice", path, "Choose an Arcanum") };
                    choices.Add(new() { Id = key, Path = path, Mode = BuildChoiceMode.Multi, Choose = 1, Options = options.Select(s => new BuildChoiceOption { Value = ChoiceValue.Of(s), Label = s }).ToArray(), PreservedSelection = CanonicalFixtures.Choice(selected), Status = valid ? BuildChoiceStatus.Resolved : BuildChoiceStatus.Unresolved, Issues = own });
                }
            var warnings = c["class_options"]?["fixture_warning"]?.GetValue<bool>() == true ? new[] { new BuildIssue("unknown_history", ["general_feats"], "Review historical feat provenance.", BuildIssueSeverity.Warning) } : System.Array.Empty<BuildIssue>();
            return new(choices, choices.SelectMany(c => c.Issues).Concat(warnings).ToArray());
        }
        private FeatRuleContext FeatContext(JsonObject c) => new()
        {
            Level = c["level"]!.GetValue<int>(), Scores = c["ability_scores"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<int>()),
            OwnedFeats = (c["general_feats"] as JsonArray ?? []).OfType<JsonObject>().Select(f => new OwnedFeatSelection("feat:" + f["instance_id"]!.GetValue<string>(), f["feat_id"]?.GetValue<string>() ?? "", f["name"]!.GetValue<string>(), f["source"]?["level"]?.GetValue<int>() ?? 4, CanonicalFixtures.Choices(f["choices"] as JsonObject))).ToArray()
        };
    }
}
