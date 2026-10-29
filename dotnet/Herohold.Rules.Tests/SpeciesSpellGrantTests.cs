using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
internal static class SpellGrantFixture
{
    internal static string Root()
    {var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;return root!.FullName;}
    internal static string Read(string name)=>File.ReadAllText(Path.Combine(Root(),"data/imported/5etools",name+".json"));
    internal static RulesCatalogSnapshot Snapshot()=>RulesCatalogSnapshot.Parse(Read("classes"),Read("species"),Read("backgrounds"),Read("feats"),Read("spells"),Read("equipment"),Read("weapons"),File.ReadAllText(Path.Combine(Root(),"data/raw/5etools/spells/spells-xphb.json")));
    internal static RawSpeciesSpellCatalog Species()=>RawSpeciesSpellCatalog.Parse(File.ReadAllText(Path.Combine(Root(),"data/raw/5etools/races.json")),Snapshot());
}
[TestCategory("RulesData")][TestClass] public sealed class SpeciesSpellGrantTests
{
    [TestMethod] public void DrowLevelGatesPaymentSourceAbilityAndExistingAliasStayCompatible()
    {
        var input=JsonNode.Parse("""{"species":"Elf","level":5,"species_options":{"lineage":"Drow","lineage_spellcasting_ability":"Charisma","lineage_spells":[]}}""")!.AsObject();var before=input.ToJsonString();
        var result=SpellGrantFixture.Species().Derive(input);
        CollectionAssert.AreEqual(new[]{"Dancing Lights","Faerie Fire","Darkness"},result.AutomaticSpells.ToArray());
        var darkness=result.Metadata.Single(grant=>grant.SpellName=="Darkness");Assert.AreEqual("grant-685fc515953953501159899e",darkness.Id);Assert.AreEqual(Herohold.Core.Ability.Charisma,darkness.Ability);Assert.AreEqual(1,darkness.MaxUses);Assert.IsTrue(darkness.AllowsSlots);Assert.AreEqual("long_rest",darkness.Recovery);
        Assert.AreEqual(before,input.ToJsonString());input["level"]=1;Assert.IsFalse(SpellGrantFixture.Species().Derive(input).Metadata.Any(grant=>grant.SpellName=="Darkness"));
    }
    [TestMethod] public void ForestGnomeUsesProficiencyAndHighElfRetainsOnlyChosenCantripPlusCurrentAutomaticGrants()
    {
        var gnome=JsonNode.Parse("""{"species":"Gnome","level":1,"species_options":{"gnomish_lineage":"Forest Gnome","lineage_spellcasting_ability":"Intelligence"}}""")!.AsObject();
        Assert.AreEqual(2,SpellGrantFixture.Species().Derive(gnome).Metadata.Single(grant=>grant.SpellName=="Speak with Animals").MaxUses);
        var elf=JsonNode.Parse("""{"species":"Elf","level":3,"folio_species_auto_grants":["Dancing Lights","Faerie Fire","Darkness"],"species_options":{"lineage":"High Elf","lineage_spellcasting_ability":"Wisdom","lineage_spells":["Fire Bolt","Dancing Lights","Darkness"]}}""")!.AsObject();
        var result=SpellGrantFixture.Species().Derive(elf);CollectionAssert.AreEqual(new[]{"Fire Bolt","Detect Magic"},result.SelectedSpells.ToArray());
        Assert.IsTrue(result.Metadata.Single(grant=>grant.SpellName=="Fire Bolt").AtWill);
    }
    [TestMethod] public void UnresolvedSpeciesCastingAbilityWarnsWithoutInventingACaster()
    {
        var input=JsonNode.Parse("""{"species":"Elf","level":5,"species_options":{"lineage":"Drow"}}""")!.AsObject();
        var result=SpellGrantFixture.Species().Derive(input);Assert.AreEqual(0,result.Metadata.Count);Assert.IsTrue(result.Warnings.Count>0);Assert.IsTrue(result.AutomaticSpells.Contains("Darkness"));
    }
}
