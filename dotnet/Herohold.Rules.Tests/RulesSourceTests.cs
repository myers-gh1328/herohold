using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;

/// <summary>The species source records and general rules tables, read typed when they load.</summary>
[TestClass] public sealed class RulesSourceTests
{
    [TestCategory("RulesData")][TestMethod] public void SpeciesSourcesReadLineagesAndTheirSpellsTyped()
    {
        var sources=SpeciesSourceCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/races.json")));
        var elf=sources.Find("elf")!;
        Assert.AreEqual(("Elf",60),(elf.Name,elf.Darkvision));
        var drow=elf.Version("Drow")!;
        Assert.AreEqual(120,drow.Darkvision);
        Assert.IsNull(elf.Version("High Elf")!.Darkvision,"A lineage that does not set darkvision takes its species'.");
        Assert.IsNull(elf.Version(""),"No lineage is chosen.");
        var spells=drow.AdditionalSpells!.Single();
        CollectionAssert.AreEqual(new[]{"int","wis","cha"},spells.AbilityChoices!.ToArray());
        Assert.AreEqual("dancing lights|xphb#c",spells.Known.Single(g=>g.Gate=="1").Spells.Single().Spell);
        var level3=spells.Innate.Single(g=>g.Gate=="3");
        Assert.AreEqual(("1","faerie fire|xphb"),(level3.Daily.Single().Key,level3.Daily.Single().Value.Single().Spell));
        Assert.IsNull(sources.Find("Unknown"));
    }
    [TestMethod] public void SpeciesSourceListsKeepPlainNamesAndSkipChoices()
    {
        var sources=SpeciesSourceCatalog.Parse("""{"race":[{"name":"Fixture","source":"XPHB","resist":["fire",{"choose":{"from":["cold"]}}],"additionalSpells":[{"known":{"_":[{"choose":"level=0|class=Wizard"}]}}]},{"name":"Fixture","source":"OTHER"}]}""");
        var fixture=sources.Find("Fixture")!;
        CollectionAssert.AreEqual(new[]{"fire"},fixture.Resist!.ToArray());
        Assert.IsNull(fixture.Immune);
        Assert.AreEqual(("","level=0|class=Wizard"),(fixture.AdditionalSpells!.Single().Known.Single().Spells.Single().Spell,fixture.AdditionalSpells![0].Known[0].Spells[0].Choose));
        Assert.Throws<ArgumentException>(()=>SpeciesSourceCatalog.Parse("""{"race":[{"name":"Twin","source":"XPHB"},{"name":"twin","source":"XPHB"}]}"""));
        Assert.Throws<ArgumentException>(()=>SpeciesSourceCatalog.Parse("""{"race":{}}"""));
    }
    [TestCategory("RulesData")][TestMethod] public void RulesTablesReadSkillsOriginFeatsAndDescriptions()
    {
        var rules=RulesReference.Parse(SpellGrantFixture.Read("rules"));
        Assert.AreEqual(KeyValuePair.Create("Acrobatics",Ability.Dexterity),rules.Skills[0]);
        Assert.AreEqual(18,rules.Skills.Count);
        CollectionAssert.Contains(rules.OriginFeats.ToArray(),"Alert");
        Assert.IsTrue(rules.FeatureDescriptions.ContainsKey("Archery"));
        Assert.Throws<ArgumentException>(()=>RulesReference.Parse("""{"skills":{"Arcana":"Luck"}}"""));
        Assert.Throws<ArgumentException>(()=>RulesReference.Parse("[]"));
    }
}
