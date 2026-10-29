using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;

[TestClass]
public sealed class ClassProgressionTests
{
    private static ClassProgressionCatalog Catalog() => ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "classes.json")));

    [TestCategory("RulesData")]
    [TestMethod]
    public void CatalogReadsReviewedClassTablesAndReturnsDetachedSnapshots()
    {
        var catalog = Catalog();
        Assert.AreEqual(12, catalog.Classes.Count);
        var fighter = ClassProgressionRules.AtLevel(catalog, "Fighter", 5);
        Assert.AreEqual(10, fighter.HitDie);
        Assert.AreEqual("XPHB", fighter.Source);
        Assert.AreEqual(3, fighter.Resources.Count("second_wind"));
        Assert.IsTrue(fighter.FeaturesAtLevel.Contains("Extra Attack"));
        Assert.IsTrue(fighter.FeaturesUpToLevel.Contains("Second Wind"));
        Assert.IsNull(fighter.Spellcasting);
        Assert.IsTrue(fighter.ChoicesAtLevel.Count > 0);
        Assert.Throws<ArgumentException>(() => ClassProgressionRules.AtLevel(catalog, "Unknown", 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClassProgressionRules.AtLevel(catalog, "Fighter", 21));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void SpellcastingTablesKeepWizardBookPactSlotsAndThirdCasterThresholdsDistinct()
    {
        var catalog = Catalog();
        var wizard = ClassProgressionRules.AtLevel(catalog, "Wizard", 5).Spellcasting!;
        Assert.IsNotNull(wizard);
        Assert.AreEqual(Herohold.Core.Ability.Intelligence, wizard.Ability);
        Assert.AreEqual(14, wizard.SpellbookLimit);
        Assert.AreEqual(0, wizard.KnownLimit);
        Assert.AreEqual(9, wizard.PreparedLimit);
        Assert.AreEqual(4, wizard.CantripLimit);
        Assert.AreEqual(2, wizard.Slots[3]);
        var warlock = ClassProgressionRules.AtLevel(catalog, "Warlock", 11).Spellcasting!;
        Assert.AreEqual("pact", warlock.CastingType);
        Assert.AreEqual(3, warlock.Slots[5]);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, warlock.AvailableSpellLevels.ToArray());
        Assert.IsNull(ClassProgressionRules.AtLevel(catalog, "Fighter", 2, "Eldritch Knight").Spellcasting);
        var knight = ClassProgressionRules.AtLevel(catalog, "Fighter", 3, "Eldritch Knight").Spellcasting!;
        Assert.AreEqual(2, knight.Slots[1]);
        Assert.AreEqual(3, knight.PreparedLimit);
        var trickster = ClassProgressionRules.AtLevel(catalog, "Rogue", 3, "Arcane Trickster").Spellcasting!;
        Assert.IsTrue(trickster.GrantedCantrips.Contains("Mage Hand"));
        Assert.AreEqual(2, trickster.CantripLimit);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void SubclassGrantsAccumulateWithSourcesWithoutAliasingTheCatalog()
    {
        var catalog = Catalog();
        var ranger = ClassProgressionRules.AtLevel(catalog, "Ranger", 5, "Gloom Stalker");
        Assert.IsTrue(ranger.SubclassGrants.AlwaysPreparedSpells.Contains("Disguise Self"));
        Assert.IsTrue(ranger.SubclassGrants.AlwaysPreparedSpells.Contains("Rope Trick"));
        Assert.IsTrue(ranger.SubclassGrants.SpellGrants.Count > 0);
        Assert.IsTrue(ranger.SubclassGrants.SpellGrants.All(g => g.SubclassName == "Gloom Stalker" && g.Name.Length > 0), "Each grant names its spell and its subclass.");
    }

    [TestMethod]
    public void CatalogRejectsDuplicateClassNamesAndMalformedSubclassRecords()
    {
        Assert.Throws<ArgumentException>(() => ClassProgressionCatalog.Parse("[{\"name\":\"Test\",\"hit_die\":8},{\"name\":\"test\",\"hit_die\":8}]"));
        Assert.Throws<ArgumentException>(() => ClassProgressionCatalog.Parse("[{\"name\":\"Test\",\"hit_die\":8,\"subclasses\":[42]}]"));
        Assert.Throws<ArgumentException>(() => ClassProgressionCatalog.Parse("[]"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void CanonicalSubclassNamesRetainFullIdentityWhileShortNamesRemainAliases()
    {
        var catalog = Catalog();
        var full = ClassProgressionRules.AtLevel(catalog, "Cleric", 3, "Life Domain");
        var alias = ClassProgressionRules.AtLevel(catalog, "Cleric", 3, "Life");
        Assert.AreEqual("Life Domain", full.SubclassName);
        Assert.AreEqual("Life Domain", alias.SubclassName);
        Assert.IsTrue(catalog.Classes.Single(x => x.Name == "Cleric").Subclasses.Contains("Life Domain"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void LegacyEarlySubclassGrantRecordsStayInactiveUntilTheClassChoiceThreshold()
    {
        var catalog = Catalog();
        foreach (var level in new[] { 1, 2 })
        {
            var early = ClassProgressionRules.AtLevel(catalog, "Warlock", level, "Celestial Patron");
            Assert.AreEqual("Celestial Patron", early.SubclassName);
            Assert.AreEqual(0, early.SubclassGrants.GrantedCantrips.Count);
            Assert.AreEqual(0, early.SubclassGrants.SpellGrants.Count);
            Assert.AreEqual(0, early.SubclassFeaturesUpToLevel.Count);
            Assert.IsNotNull(early.Spellcasting);
        }
        Assert.IsTrue(ClassProgressionRules.AtLevel(catalog, "Warlock", 3, "Celestial Patron").SubclassGrants.GrantedCantrips.Count > 0);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void ClassRecordsReadTheCatalogsNestedTablesTyped()
    {
        var catalog = Catalog();
        var warlock = catalog.Records.Single(c => c.Name == "Warlock");
        var blade = warlock.Options.EldritchInvocations!.Options.Single(o => o.Name == "Devouring Blade");
        Assert.AreEqual(12, blade.MinimumWarlockLevel);
        CollectionAssert.AreEqual(new[] { "Pact of the Blade", "Thirsting Blade" }, blade.RequiredInvocations.ToArray());
        var abjurer = catalog.Records.Single(c => c.Name == "Wizard").Subclass("Abjurer")!;
        var savant = abjurer.Options.SavantSpellChoicesByLevel[5];
        Assert.AreEqual(("A", 3, 1), (savant.School, savant.MaximumSpellLevel, savant.Choose));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, savant.SpellLevels.ToArray());
        var arid = catalog.Records.Single(c => c.Name == "Druid").Subclass("Land")!.Options.StructuredSpellChoice!.Options.Single(o => o.Name == "Arid Land");
        CollectionAssert.AreEqual(new[] { "Blur", "Burning Hands", "Fire Bolt" }, arid.GrantedSpellsByLevel[3].ToArray());
        var barbarian = catalog.Records.Single(c => c.Name == "Barbarian");
        Assert.AreEqual(2, barbarian.Level1Rules.Count("rages"));
        Assert.AreEqual("10 + Dexterity + Constitution", barbarian.Level1Rules.Dice["armor_class_formula"]);
        Assert.AreEqual("subclass_choice", barbarian.Progression[3].Features.Single(f => f.Type == "subclass_choice").Type);
    }

    [TestMethod]
    public void MalformedClassTablesFailWhenTheCatalogLoads()
    {
        foreach (var bad in new[]
        {
            "{\"progression\":{\"one\":{\"features\":[]}}}", "{\"progression\":{\"21\":{\"features\":[]}}}",
            "{\"level_1_rules\":{\"spell_slots\":{\"10\":1}}}", "{\"level_1_rules\":{\"rages\":true}}",
            "{\"class_options\":{\"unknown_option\":[]}}", "{\"spellcasting\":{\"ability\":\"Luck\"}}",
            "{\"subclasses\":[{\"name\":\"Sub\",\"subclass_options\":{\"unknown_option\":{}}}]}",
            "{\"subclasses\":[{\"name\":\"Sub\",\"grants_by_level\":{\"3\":{\"spell_grants\":[\"Light\"]}}}]}"
        })
        {
            var json = "[{\"name\":\"Test\",\"hit_die\":8," + bad[1..] + "]";
            Assert.Throws<ArgumentException>(() => ClassProgressionCatalog.Parse(json), bad);
        }
    }
}
