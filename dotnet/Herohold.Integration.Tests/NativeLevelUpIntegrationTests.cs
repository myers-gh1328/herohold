using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativeLevelUpIntegrationTests
{
    private static NativeLevelUpRules Rules(NativeCharacterBuilder builder)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "data/imported/5etools/classes.json"))) root = root.Parent;
        var catalog = NativeCatalogIntegrationTests.Snapshot();var classes = ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(root!.FullName, "data/imported/5etools/classes.json")));
        return new(catalog, classes, NativeCharacterBuilderTests.Feats(), new(catalog), builder);
    }

    [TestMethod]
    public void FirstNativeCalculationIncludesFixedBackgroundAndSpeciesHpFeatures()
    {
        var builder = NativeCharacterBuilderTests.Builder();
        foreach (var (species, background, expected) in new[] { ("Dwarf", "Soldier", 11), ("Orc", "Farmer", 12) })
        {
            var source = NativeCharacterBuilderTests.Fighter();source["species"] = species;source["background"] = background;
            if (background == "Farmer") source["background_options"] = new JsonObject { ["primary_ability"] = "Strength", ["secondary_ability"] = "Constitution" };
            var first = builder.Recalculate(source);Assert.AreEqual(expected, first["hit_points"]!["maximum"]!.GetValue<int>(), species + "/" + background);
            Assert.IsTrue(JsonNode.DeepEquals(first, builder.Recalculate(first)), "A second calculation must not discover an omitted HP feature.");
        }
    }

    [TestMethod]
    public void ActualFighterRulesAdvanceThroughSubclassAsiMasteryAndRetroactiveConstitution()
    {
        var builder = NativeCharacterBuilderTests.Builder();var rules = Rules(builder);var source = builder.Recalculate(NativeCharacterBuilderTests.Fighter());
        
        var second = rules.Commit(source, rules.Start(source, 2).Draft).Character;Assert.AreEqual(16, second["hit_points"]!["maximum"]!.GetValue<int>());
        var third = rules.Start(second, 3);var subclass = third.PendingChoices.Single(c => c.Path.SequenceEqual(new[] { "subclass" }));
        third = rules.Choose(second, third.Draft, subclass.Id, JsonValue.Create("Champion"));Assert.IsTrue(third.CanFinalize, string.Join("; ", third.Issues.Select(i => i.Message)));
        var level3 = rules.Commit(second, third.Draft).Character;var fourth = rules.Start(level3, 4);
        var asi = fourth.PendingChoices.Single(c => c.Mode == BuildChoiceMode.AsiOrFeat);
        fourth = rules.Choose(level3, fourth.Draft, asi.Id, JsonNode.Parse("""{"mode":"asi","ability_increases":{"Constitution":2}}"""));
        var mastery = fourth.PendingChoices.Single(c => c.Id == "fighter_4_weapon_mastery");Assert.AreEqual(1, mastery.Choose);
        fourth = rules.Choose(level3, fourth.Draft, mastery.Id, new JsonArray("Dagger"));Assert.IsTrue(fourth.CanFinalize, string.Join("; ", fourth.Issues.Select(i => i.Message)));
        var result = rules.Commit(level3, fourth.Draft).Character;
        Assert.AreEqual(32, result["hit_points"]!["maximum"]!.GetValue<int>());
        Assert.AreEqual(1, result["ability_score_adjustments"]!.AsArray().Count);Assert.AreEqual(3, result["completed_level_up_transactions"]!.AsArray().Count);
        Assert.IsTrue(builder.ResultFor(result)["validation"]!["complete"]!.GetValue<bool>());
    }

    [TestMethod]
    public void ActualWizardAddsScholarAndTwoBookSpellsWithoutReplacingPreparedSpellsOrFreeGrants()
    {
        var builder = NativeCharacterBuilderTests.Builder();var rules = Rules(builder);var source = builder.Recalculate(NativeCharacterBuilderTests.Wizard());
        var start = rules.Start(source, 2);
        Assert.IsFalse(start.PendingChoices.Any(c => c.Id == "wizard_2_cantrips_replacement"));
        var scholar = start.PendingChoices.Single(c => c.Path.LastOrDefault() == "wizard_scholar_skill");start = rules.Choose(source, start.Draft, scholar.Id, JsonValue.Create("Investigation"));
        var book = start.PendingChoices.Single(c => c.Id == "wizard_2_spellbook_spells");Assert.AreEqual(2, book.Choose);
        Assert.AreEqual(0, NativeChoiceProjection.ToJson(book)["value"]!.AsArray().Count, "The editor must receive new additions, not the full existing book.");
        start = rules.Choose(source, start.Draft, book.Id, new JsonArray("Burning Hands", "Feather Fall"));Assert.IsTrue(start.CanFinalize, string.Join("; ", start.Issues.Select(i => i.Message)));
        Assert.AreEqual(2, NativeChoiceProjection.ToJson(start.PendingChoices.Single(c => c.Id == book.Id))["value"]!.AsArray().Count);
        var final = rules.Commit(source, start.Draft).Character;
        Assert.AreEqual(8, final["spells"]!["spellbook"]!.AsArray().Count);Assert.AreEqual("Shield", final["spells"]!["prepared"]![0]!.GetValue<string>());
        Assert.AreEqual("Wisdom", final["origin_feat_choices"]!["spellcasting_ability"]!.GetValue<string>());
        Assert.IsTrue(builder.ResultFor(final)["validation"]!["complete"]!.GetValue<bool>());
    }

    [TestMethod]
    public void MagicInitiateSpellReplacementIsExplicitSameLevelAndKeepsItsCastingAbility()
    {
        var builder = NativeCharacterBuilderTests.Builder();var rules = Rules(builder);var source = builder.Recalculate(NativeCharacterBuilderTests.Wizard());
        var review = rules.Start(source, 2);
        review = rules.Choose(source, review.Draft, review.PendingChoices.Single(c => c.Path.LastOrDefault() == "wizard_scholar_skill").Id, JsonValue.Create("Investigation"));
        review = rules.Choose(source, review.Draft, "wizard_2_spellbook_spells", new JsonArray("Burning Hands", "Feather Fall"));
        var control = review.PendingChoices.Single(c => c.Id == "wizard_2_magic_initiate_wizard_replacement");Assert.AreEqual("keep", control.PreservedSelection!.Text);
        review = rules.Choose(source, review.Draft, control.Id, JsonValue.Create("Shield"));Assert.IsFalse(review.CanFinalize);
        var spell = review.PendingChoices.Single(c => c.Id == "wizard_2_magic_initiate_wizard_replace_with");
        Assert.IsTrue(spell.Options.Any(o => o.Value.Text == "Magic Missile"));Assert.IsFalse(spell.Options.Any(o => o.Value.Text == "Bless"));
        var invalid = rules.Choose(source, review.Draft, spell.Id, JsonValue.Create("Fire Bolt"));Assert.IsFalse(invalid.CanFinalize);
        var valid = rules.Choose(source, invalid.Draft, spell.Id, JsonValue.Create("Magic Missile"));Assert.IsTrue(valid.CanFinalize, string.Join("; ", valid.Issues.Select(i => i.Message)));
        Assert.AreEqual("Magic Missile", valid.Character["origin_feat_choices"]!["spell"]!.GetValue<string>());Assert.AreEqual("Wisdom", valid.Character["origin_feat_choices"]!["spellcasting_ability"]!.GetValue<string>());
        Assert.AreEqual("Shield", source["origin_feat_choices"]!["spell"]!.GetValue<string>());
        Assert.AreEqual("Shield", valid.Character["spells"]!["prepared"]![0]!.GetValue<string>(), "Replacing a feat spell must not replace the ordinary prepared source.");
    }
}
