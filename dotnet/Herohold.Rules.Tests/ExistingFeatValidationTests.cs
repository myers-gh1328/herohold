using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Rules.Tests;

[TestClass]
public sealed class ExistingFeatValidationTests
{
    private static readonly Lazy<RulesCatalogSnapshot> Catalog = new(() =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);while (root is not null && !File.Exists(Path.Combine(root.FullName, "data/imported/5etools/classes.json"))) root = root.Parent;
        string Read(string name) => File.ReadAllText(Path.Combine(root!.FullName, "data/imported/5etools", name + ".json"));
        return RulesCatalogSnapshot.Parse(Read("classes"), Read("species"), Read("backgrounds"), Read("feats"), Read("spells"), Read("equipment"), Read("weapons"), File.ReadAllText(Path.Combine(root!.FullName, "data/raw/5etools/spells/spells-xphb.json")));
    });
    private static ExistingFeatValidationRules Rules() => new(Catalog.Value, new(Catalog.Value));
    private static JsonObject Feat(string id, string name, string instance = "owned", int? level = 4, JsonObject? choices = null) => new()
    {
        ["instance_id"] = instance, ["feat_id"] = id, ["name"] = name,
        ["source"] = level is null ? new JsonObject { ["type"] = "legacy" } : new JsonObject { ["type"] = "class_level", ["class_name"] = "Fighter", ["level"] = level },
        ["choices"] = choices ?? new JsonObject { ["ability"] = "Charisma" }
    };
    private static JsonObject Character(params JsonObject[] feats)
    {
        var scores = new JsonObject();foreach (var ability in Enum.GetValues<Ability>()) scores[ability.ToString()] = 14;
        return new() { ["class"] = "Fighter", ["level"] = 8, ["species"] = "Orc", ["background"] = "", ["base_ability_scores"] = scores,
            ["class_options"] = new JsonObject(), ["species_options"] = new JsonObject(), ["background_options"] = new JsonObject(), ["general_feats"] = new JsonArray(feats.Select(f => (JsonNode)f.DeepClone()).ToArray()), ["ability_score_adjustments"] = new JsonArray() };
    }
    private static void Linked(JsonObject c, string instance, string ability, int level = 4)
    {
        c["ability_score_adjustments"]!.AsArray().Add(new JsonObject { ["type"] = "ability_score_adjustment", ["source_type"] = "feat", ["feat_instance_id"] = instance, ["source_level"] = level,
            ["source"] = new JsonObject { ["type"] = "class_level", ["class_name"] = "Fighter", ["level"] = level }, ["adjustments"] = new JsonObject { [ability] = 1 }, ["maximum"] = 20 });
    }
    private static FeatRuleContext Context(JsonObject c, IReadOnlyList<ChoiceProficiencySource>? sources = null, IReadOnlyDictionary<string, int>? levels = null)
    {
        var scores = c["base_ability_scores"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<int>());
        foreach (var adjustment in c["ability_score_adjustments"]!.AsArray().OfType<JsonObject>()) foreach (var pair in adjustment["adjustments"]!.AsObject()) scores[pair.Key] += pair.Value!.GetValue<int>();
        return new() { Level = c["level"]!.GetValue<int>(), Scores = scores, HasSpellcastingFeature = false, ProficiencySources = sources ?? [], SourceLevels = levels ?? new Dictionary<string, int>(),
            OwnedFeats = c["general_feats"]!.AsArray().OfType<JsonObject>().Select(f => new OwnedFeatSelection("feat:" + f["instance_id"]!.GetValue<string>(), f["feat_id"]?.GetValue<string>() ?? "", f["name"]!.GetValue<string>(), f["source"]?["level"]?.GetValue<int>() ?? 1, CanonicalFixtures.Choices(f["choices"] as JsonObject))).ToArray() };
    }
    /// <summary>The fixture as the typed build stores it, for comparing with a rule's output.</summary>
    private static string Stored(JsonObject c) => BuildFacts.FromCanonical(c).ReplaceIn(c, "fixture").ToJsonString();
    private static bool Errors(IEnumerable<BuildIssue> issues) => issues.Any(i => i.Severity == BuildIssueSeverity.Error);

    [TestCategory("RulesData")]
    [TestMethod]
    public void InvalidSkillExpertChildrenBlockAdoptionAndStructuredRepairKeepsIdentityAndLinkedSource()
    {
        var c = Character(Feat("skill_expert_xphb", "Skill Expert", choices: new() { ["ability"] = "Wisdom", ["skill_proficiency"] = "Insight", ["expertise"] = "Arcana" }));Linked(c, "owned", "Wisdom");
        var sources = new[] { new ChoiceProficiencySource("base", ProficiencySourceKind.Skill, "Stealth"), new("feat:owned", ProficiencySourceKind.Skill, "Insight") };
        var context = Context(c, sources, new Dictionary<string, int> { ["base"] = 1, ["feat:owned"] = 4 });var before = c.ToJsonString();
        Assert.IsTrue(Errors(Rules().Validate(c, context)));
        var descriptor = Rules().DescribeChoices(c, context).Single();Assert.AreEqual("owned", descriptor.InstanceId);Assert.AreEqual(4, descriptor.KnownTakenLevel);
        var expertise = descriptor.Fields.Single(f => f.Id == "expertise");CollectionAssert.AreEqual(new[] { "feat_choices", "expertise" }, expertise.Path.ToArray());
        Assert.IsTrue(expertise.Options.Any(o => o.Value.Text == "Insight"));
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Wisdom", ["skill_proficiency"] = "Insight", ["expertise"] = "Insight" });
        Assert.IsFalse(Errors(Rules().Validate(repaired, Context(repaired, sources, context.SourceLevels))));
        Assert.AreEqual(before, c.ToJsonString());Assert.AreEqual("owned", repaired["general_feats"]![0]!["instance_id"]!.GetValue<string>());
        Assert.AreEqual(c["general_feats"]![0]!["source"]!.ToJsonString(), repaired["general_feats"]![0]!["source"]!.ToJsonString());
        Assert.AreEqual(1, repaired["ability_score_adjustments"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void IdNameConflictsDuplicateNonrepeatableFeatsAndMalformedChoicesAreErrors()
    {
        var conflict = Character(Feat("actor_xphb", "Skill Expert"));Assert.IsTrue(Rules().Validate(conflict, Context(conflict)).Any(i => i.Code == "feat_identity"));
        var duplicate = Character(Feat("actor_xphb", "Actor", "first"), Feat("actor_xphb", "Actor", "second"));Assert.IsTrue(Rules().Validate(duplicate, Context(duplicate)).Any(i => i.Code == "duplicate_feat"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void AFeatCannotUseItsOwnAbilityIncreaseToMeetItsKnownOriginalPrerequisite()
    {
        var c = Character(Feat("actor_xphb", "Actor"));c["base_ability_scores"]!["Charisma"] = 12;Linked(c, "owned", "Charisma");
        Assert.IsTrue(Rules().Validate(c, Context(c, levels: new Dictionary<string, int> { ["feat:owned"] = 4 })).Any(i => i.Code == "prerequisites" && i.Severity == BuildIssueSeverity.Error));
        c["base_ability_scores"]!["Charisma"] = 13;Assert.IsFalse(Errors(Rules().Validate(c, Context(c, levels: new Dictionary<string, int> { ["feat:owned"] = 4 }))));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow(4, false)]
    [DataRow(19, true)]
    public void ResilientBeforeMonkFourteenRemainsValidWhileTakingItAfterAlreadyHavingAllSavesDoesNot(int taken, bool invalid)
    {
        var c = Character(Feat("resilient_xphb", "Resilient", level: taken, choices: new() { ["ability"] = "Wisdom" }));c["class"] = "Monk";c["level"] = 20;Linked(c, "owned", "Wisdom", taken);
        var sources = Enum.GetValues<Ability>().Select(a => new ChoiceProficiencySource("class_feature:Disciplined Survivor", ProficiencySourceKind.Save, a.ToString())).Append(new("feat:owned", ProficiencySourceKind.Save, "Wisdom")).ToArray();
        var context = Context(c, sources, new Dictionary<string, int> { ["class_feature:Disciplined Survivor"] = 14, ["feat:owned"] = taken });
        Assert.AreEqual(invalid, Errors(Rules().Validate(c, context)));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void LegacyUnknownHistoryAndUnknownHomebrewStayVisibleAsWarningsWithoutInventingGainLevels()
    {
        var c = Character(Feat("actor_xphb", "Actor", level: null), Feat("custom", "Unmodeled Homebrew", "custom", null, new()));
        var issues = Rules().Validate(c, Context(c));Assert.IsFalse(Errors(issues));Assert.IsTrue(issues.Any(i => i.Code == "unknown_history"));Assert.IsTrue(issues.Any(i => i.Code == "unknown_feat"));
        Assert.IsNull(Rules().DescribeChoices(c, Context(c)).Single(f => f.InstanceId == "owned").KnownTakenLevel);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void AbilityRepairUpdatesOneLinkedRecordAndPreservesInvalidRawChoiceWithoutStacking()
    {
        var c = Character(Feat("resilient_xphb", "Resilient", choices: new() { ["ability"] = "Wisdom" }));Linked(c, "owned", "Wisdom");
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Constitution" });
        Assert.AreEqual(1, repaired["ability_score_adjustments"]!.AsArray().Count);
        Assert.AreEqual(1, repaired["ability_score_adjustments"]![0]!["adjustments"]!["Constitution"]!.GetValue<int>());Assert.IsNull(repaired["ability_score_adjustments"]![0]!["adjustments"]!["Wisdom"]);
        Assert.AreEqual(c["ability_score_adjustments"]![0]!["source"]!.ToJsonString(), repaired["ability_score_adjustments"]![0]!["source"]!.ToJsonString());
        var invalid = Rules().ApplyChoices(repaired, "owned", new() { ["ability"] = "Luck" });Assert.AreEqual("Luck", invalid["general_feats"]![0]!["choices"]!["ability"]!.GetValue<string>());
        Assert.AreEqual(repaired["ability_score_adjustments"]!.ToJsonString(), invalid["ability_score_adjustments"]!.ToJsonString());Assert.IsTrue(Errors(Rules().Validate(invalid, Context(invalid))));
        var repeat = Rules().ApplyChoices(repaired, "owned", new() { ["ability"] = "Constitution" });Assert.AreEqual(1, repeat["ability_score_adjustments"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void UnlinkedLegacyIncreaseIsNotAddedUnlessTheUserExplicitlyChoosesToApplyIt()
    {
        var c = Character(Feat("resilient_xphb", "Resilient", level: null, choices: new() { ["ability"] = "Wisdom" }));
        var unchanged = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Wisdom" });Assert.AreEqual(0, unchanged["ability_score_adjustments"]!.AsArray().Count);
        Assert.IsTrue(Rules().Validate(unchanged, Context(unchanged)).Any(i => i.Code == "unlinked_ability_increase" && i.Severity == BuildIssueSeverity.Warning));
        var explicitRepair = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Wisdom", ["apply_ability_increase"] = true });Assert.AreEqual(1, explicitRepair["ability_score_adjustments"]!.AsArray().Count);
        var again = Rules().ApplyChoices(explicitRepair, "owned", new() { ["ability"] = "Wisdom", ["apply_ability_increase"] = true });Assert.AreEqual(1, again["ability_score_adjustments"]!.AsArray().Count);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void DescribedFieldsAndChoicesAreDetachedFromTheCanonicalSource()
    {
        var c = Character(Feat("skill_expert_xphb", "Skill Expert", choices: new() { ["ability"] = "Wisdom", ["skill_proficiency"] = "Insight", ["expertise"] = "Insight" }));var before = c.ToJsonString();
        var choice = Rules().DescribeChoices(c, Context(c)).Single();
        foreach (var field in choice.Fields) field.PreservedSelection?.Fields?.Add("probe", ChoiceValue.Of(true));
        Assert.AreEqual(before, c.ToJsonString());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void UnknownChildKeysAndAmbiguousLinkedRaisesCannotSilentlyBecomeValid()
    {
        var c = Character(Feat("actor_xphb", "Actor", choices: new() { ["ability"] = "Charisma", ["invented_permission"] = "anything" }));
        Assert.IsTrue(Rules().Validate(c, Context(c)).Any(i => i.Code == "unknown_choice"));
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Charisma" });
        Assert.IsFalse(Errors(Rules().Validate(repaired, Context(repaired))));Assert.IsNull(repaired["general_feats"]![0]!["choices"]!["invented_permission"]);
        Assert.AreEqual("anything", repaired["general_feats"]![0]!["folio_legacy_choice_history"]![0]!["invented_permission"]!.GetValue<string>());
        c["general_feats"]![0]!["choices"]!.AsObject().Remove("invented_permission");Linked(c, "owned", "Charisma");Linked(c, "owned", "Charisma");
        Assert.IsTrue(Rules().Validate(c, Context(c)).Any(i => i.Code == "ambiguous_ability_increase"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void UnknownHistoricalResilientDoesNotBecomeIllegalBecauseOfLaterAutomaticSaves()
    {
        var c = Character(Feat("resilient_xphb", "Resilient", level: null, choices: new() { ["ability"] = "Wisdom" }));c["class"] = "Monk";c["level"] = 20;
        var sources = Enum.GetValues<Ability>().Select(a => new ChoiceProficiencySource("class_feature:Disciplined Survivor", ProficiencySourceKind.Save, a.ToString())).ToArray();
        var context = Context(c, sources, new Dictionary<string, int> { ["class_feature:Disciplined Survivor"] = 14 });
        Assert.IsFalse(Errors(Rules().Validate(c, context)));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void HistoricalAsiValidatesItsExplicitAllocationAndRepairDoesNotStack()
    {
        var c = Character(Feat("ability_score_improvement_xphb", "Ability Score Improvement", choices: new() { ["allocation_mode"] = "split", ["primary_ability"] = "Wisdom", ["secondary_ability"] = "Constitution" }));
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["allocation_mode"] = "split", ["primary_ability"] = "Wisdom", ["secondary_ability"] = "Constitution", ["apply_ability_increase"] = true });
        Assert.IsFalse(Errors(Rules().Validate(repaired, Context(repaired))));Assert.AreEqual(1, repaired["ability_score_adjustments"]!.AsArray().Count);
        Assert.AreEqual(1, repaired["ability_score_adjustments"]![0]!["adjustments"]!["Wisdom"]!.GetValue<int>());
        Assert.AreEqual(1, repaired["ability_score_adjustments"]![0]!["adjustments"]!["Constitution"]!.GetValue<int>());
        var invalid = Rules().ApplyChoices(repaired, "owned", new() { ["allocation_mode"] = "split", ["secondary_ability"] = "Wisdom" });
        Assert.IsTrue(Errors(Rules().Validate(invalid, Context(invalid))));
    }

    [TestMethod]
    public void MalformedFeatShapesAndUnknownFieldsAreRefusedWhenTheBuildIsRead()
    {
        foreach (var (field, value) in new (string, JsonNode)[] { ("source", "malformed"), ("choices", "invalid shape"), ("name", 7) })
        {
            var c = Character(Feat("actor_xphb", "Actor"));c["general_feats"]![0]![field] = value;
            Assert.Throws<ArgumentException>(() => BuildFacts.FromCanonical(c), field);
        }
        var note = Character(Feat("resilient_xphb", "Resilient", choices: new() { ["ability"] = "Wisdom" }));Linked(note, "owned", "Wisdom");
        note["ability_score_adjustments"]![0]!["unknown_safe_note"] = "keep this";Assert.Throws<ArgumentException>(() => BuildFacts.FromCanonical(note));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void LinkedBonusMustMatchTheFeatAllocationAndItsActualCap()
    {
        var c = Character(Feat("resilient_xphb", "Resilient", choices: new() { ["ability"] = "Wisdom" }));Linked(c, "owned", "Constitution");
        c["ability_score_adjustments"]![0]!["adjustments"]!["Constitution"] = 10;c["ability_score_adjustments"]![0]!["maximum"] = 30;
        Assert.IsTrue(Rules().Validate(c, Context(c)).Any(i => i.Code == "ability_increase_mismatch"));
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Wisdom" });
        Assert.IsFalse(Errors(Rules().Validate(repaired, Context(repaired))));Assert.AreEqual(20, repaired["ability_score_adjustments"]![0]!["maximum"]!.GetValue<int>());
        Assert.AreEqual(1, repaired["ability_score_adjustments"]![0]!["adjustments"]!["Wisdom"]!.GetValue<int>());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void UnprovenManualSaveHistoryDoesNotInventAHistoricalResilientConflict()
    {
        var c = Character(Feat("resilient_xphb", "Resilient", level: null, choices: new() { ["ability"] = "Wisdom" }));
        var sources = Enum.GetValues<Ability>().Select(a => new ChoiceProficiencySource("manual", ProficiencySourceKind.Save, a.ToString())).ToArray();
        var context = Context(c, sources);
        Assert.IsFalse(Errors(Rules().Validate(c, context)));Assert.IsTrue(Rules().Validate(c, context).Any(i => i.Code == "unknown_source_history"));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void KnownFeatIdentityNormalizationIsDetachedAndCannotHideConflicts()
    {
        var c = Character(Feat("", "skill expert", choices: new() { ["ability"] = "Wisdom", ["skill_proficiency"] = "Insight", ["expertise"] = "Insight" }));var before = c.ToJsonString();
        var normalized = Rules().NormalizeIdentities(c);Assert.AreEqual("Skill Expert", normalized["general_feats"]![0]!["name"]!.GetValue<string>());Assert.AreEqual("skill_expert_xphb", normalized["general_feats"]![0]!["feat_id"]!.GetValue<string>());Assert.AreEqual(before, c.ToJsonString());
        var conflict = Character(Feat("actor_xphb", "Skill Expert"));Assert.AreEqual(Stored(conflict), Rules().NormalizeIdentities(conflict).ToJsonString());Assert.IsTrue(Errors(Rules().Validate(conflict, Context(conflict))));
        var unknown = Character(Feat("homebrew", "unknown homebrew", level: null));Assert.AreEqual(Stored(unknown), Rules().NormalizeIdentities(unknown).ToJsonString());
        var repaired = Rules().ApplyChoices(c, "owned", new() { ["ability"] = "Wisdom" });Assert.AreEqual("Skill Expert", repaired["general_feats"]![0]!["name"]!.GetValue<string>());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void AdditionalMagicInitiateIdentityUsesTheSameResolvedDefinition()
    {
        var catalog = Catalog.Value;var additional = new JsonObject { ["id"] = "magic_initiate_wizard_xphb", ["name"] = "Magic Initiate (Wizard)", ["prerequisites"] = new JsonObject { ["any"] = new JsonArray(new JsonObject()) } };
        var rules = new ExistingFeatValidationRules(catalog, new(catalog, [CatalogFeat.From(additional)]));
        var source = Character(Feat("magic_initiate_wizard_xphb", "magic initiate (wizard)", choices: new()));
        var normalized = rules.NormalizeIdentities(source);Assert.AreEqual("Magic Initiate (Wizard)", normalized["general_feats"]![0]!["name"]!.GetValue<string>());
        Assert.AreEqual(source["ability_score_adjustments"]!.ToJsonString(), normalized["ability_score_adjustments"]!.ToJsonString());
    }
}
