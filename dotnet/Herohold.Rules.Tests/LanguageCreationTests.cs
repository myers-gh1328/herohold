using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class LanguageCreationTests
{
    private static CreationChoiceRules Rules()
    {
        var catalog=SpellGrantFixture.Snapshot();var classes=ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes"));return new(catalog,classes,new(classes,catalog,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}")),new());
    }
    [TestMethod]public void OriginPickerRequiresTwoDistinctStandardLanguagesAndPreservesInvalidRawChoicesForRepair()
    {
        foreach(var selected in new[]{new JsonArray(),new JsonArray("Elvish","Elvish"),new JsonArray("Elvish","Infernal"),new JsonArray("Elvish","Dwarvish","Giant")})
        {
            var c=CanonicalCharacterRules.Create();c["origin_languages"]=selected;var before=c.ToJsonString();var definition=Rules().Describe(CanonicalFixtures.Creation(c)).Definitions.Single(d=>d.Id=="origin_languages");
            Assert.AreEqual(2,definition.Choose);Assert.AreEqual(BuildChoiceStatus.Unresolved,definition.Status);Assert.AreEqual(9,definition.Options.Count);Assert.IsFalse(definition.Options.Any(option=>option.Label=="Common"||option.Label=="Infernal"));Assert.AreEqual(before,c.ToJsonString());
        }
        var valid=CanonicalCharacterRules.Create();valid["origin_languages"]=new JsonArray("Elvish","Dwarvish");Assert.AreEqual(BuildChoiceStatus.Resolved,Rules().Describe(CanonicalFixtures.Creation(valid)).Definitions.Single(d=>d.Id=="origin_languages").Status);
    }
    [TestMethod]public void CharacterEditsSupportOriginLanguagesButCannotPutRareLanguagesInThatChoicePath()
    {
        var edit=new CharacterEditRules(SpellGrantFixture.Snapshot());var c=CanonicalCharacterRules.Create();var before=c.ToJsonString();
        var changed=edit.Apply(c,new(){["origin_languages"]=new JsonArray("Elvish","Dwarvish")});Assert.AreEqual(2,changed["origin_languages"]!.AsArray().Count);Assert.AreEqual(before,c.ToJsonString());
        Assert.ThrowsExactly<ArgumentException>(()=>edit.Apply(c,new(){["origin_languages"]=new JsonArray("Infernal","Elvish")}));Assert.ThrowsExactly<ArgumentException>(()=>edit.Apply(c,new(){["origin_languages"]="Elvish"}));
    }
}
