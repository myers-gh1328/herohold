using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class OwnedOriginCreationTests
{
    private static CreationChoiceRules Rules()
    {
        var catalog=SpellGrantFixture.Snapshot();var classes=ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes"));return new(catalog,classes,new(classes,catalog,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}")),RulesReference.Parse(SpellGrantFixture.Read("rules")),null,ToolCatalogTests.Actual());
    }
    [TestMethod]public void ChangingBackgroundRebindsRepeatedSkilledChoicesByOwnerAndRestoresBothOnReturn()
    {
        var c=CanonicalCharacterRules.Create();c["background"]="Scribe";c["species"]="Human";c["species_options"]!["origin_feats"]=new JsonArray("Skilled");c["origin_feat_choices"]=new JsonObject{["skills"]=new JsonArray("Arcana","History","Religion")};c["folio_origin_feat_choices"]=new JsonObject{["Skilled"]=new JsonObject{["skills"]=new JsonArray("Insight","Survival","Nature")}};
        var edit=new CharacterEditRules(SpellGrantFixture.Snapshot());var changed=edit.Apply(c,new(){["background"]="Soldier"});Assert.AreEqual("Insight",changed["origin_feat_choices"]!["skills"]![0]!.GetValue<string>());
        var restored=edit.Apply(changed,new(){["background"]="Scribe"});Assert.AreEqual("Arcana",restored["origin_feat_choices"]!["skills"]![0]!.GetValue<string>());Assert.AreEqual("Insight",restored["folio_origin_feat_choices"]!["Skilled"]!["skills"]![0]!.GetValue<string>());Assert.AreEqual("Arcana",c["origin_feat_choices"]!["skills"]![0]!.GetValue<string>());
    }
    [TestMethod]public void HumanCanRepeatBackgroundSkilledWithIndependentVisibleChoiceOwners()
    {
        var c=CanonicalCharacterRules.Create();c["class"]="Fighter";c["species"]="Human";c["background"]="Scribe";c["species_options"]!["origin_feats"]=new JsonArray("Skilled");c["origin_feat_choices"]=new JsonObject{["skills"]=new JsonArray("Arcana","History","Religion")};c["folio_origin_feat_choices"]=new JsonObject{["Skilled"]=new JsonObject{["skills"]=new JsonArray("Insight","Survival","Nature")}};
        var report=Rules().Describe(CanonicalFixtures.Creation(c));Assert.IsFalse(report.Definitions.Single(d=>d.Id=="species_origin_feats").Options.Single(option=>option.Label=="Skilled").Disabled);
        var definitions=report.Definitions.Where(d=>d.Label.StartsWith("Skilled:",StringComparison.Ordinal)).ToArray();Assert.AreEqual(2,definitions.Length);Assert.AreEqual(2,definitions.Select(d=>d.Id).Distinct().Count());Assert.IsTrue(definitions.All(d=>d.Status==BuildChoiceStatus.Resolved));Assert.AreEqual(2,definitions.Select(d=>d.Details.SourceId).Distinct().Count());
    }
    [TestMethod]public void ToolCategoryRequiresAnActualToolAndBardMonkClassChoicesHaveTheirRealQuotas()
    {
        var c=CanonicalCharacterRules.Create();c["class"]="Fighter";c["species"]="Orc";c["background"]="Soldier";c["background_options"]!["tool_proficiencies"]=new JsonArray("Any gaming set");
        var invalid=Rules().Describe(CanonicalFixtures.Creation(c)).Definitions.Single(d=>d.Id=="background_options_tools");Assert.IsTrue(invalid.Options.Any(option=>option.Label=="Dice Set"));Assert.IsFalse(invalid.Options.Any(option=>option.Label.StartsWith("Any ")));Assert.AreEqual(BuildChoiceStatus.Unresolved,invalid.Status);
        c["background_options"]!["tool_proficiencies"]=new JsonArray("Dice Set");Assert.AreEqual(BuildChoiceStatus.Resolved,Rules().Describe(CanonicalFixtures.Creation(c)).Definitions.Single(d=>d.Id=="background_options_tools").Status);
        foreach(var (name,count) in new[]{("Bard",3),("Monk",1)}){c["class"]=name;var tools=Rules().Describe(CanonicalFixtures.Creation(c)).Definitions.Single(d=>d.Id=="class_options_tools");Assert.AreEqual(count,tools.Choose);Assert.IsTrue(tools.Options.Any(option=>option.Label=="Flute"));if(name=="Monk")Assert.IsTrue(tools.Options.Any(option=>option.Label=="Smith's Tools"));}
    }
}
