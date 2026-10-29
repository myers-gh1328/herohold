using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass]public sealed class NativeLanguageIntegrationTests
{
    private static JsonObject Raw()=>new(){["character_id"]="fictional-language-owner",["class"]="Rogue",["level"]=1,["species"]="Human",["background"]="Soldier",["base_ability_scores"]=new JsonObject(Enum.GetNames<Ability>().Select(ability=>new KeyValuePair<string,JsonNode?>(ability,JsonValue.Create(10)))),["origin_languages"]=new JsonArray("Elvish","Dwarvish"),["languages"]=new JsonArray("Infernal")};
    [TestMethod]public void FullNativeBuilderKeepsManualLanguagesAndBlocksMissingOriginChoices()
    {
        var source=NativeCharacterBuilderTests.Fighter();source["origin_languages"]=new JsonArray("Elvish","Dwarvish");source["languages"]=new JsonArray("Infernal");var builder=NativeCharacterBuilderTests.Builder();
        var result=builder.ResultFor(source);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());
        var character=Herohold.TestSupport.TestStores.Calculated(source);var languages=character.Derived.Languages.ToArray();CollectionAssert.AreEquivalent(new[]{"Common","Elvish","Dwarvish","Infernal"},languages);
        source["origin_languages"]=new JsonArray();var incomplete=builder.ResultFor(source);Assert.IsFalse(incomplete["validation"]!["complete"]!.GetValue<bool>());Assert.IsTrue(incomplete["choices"]!.AsArray().Any(choice=>choice!["id"]!.GetValue<string>()=="origin_languages"&&choice["status"]!.GetValue<string>()=="unresolved"));
    }
    [TestMethod]public void AnImportKeepsUnexplainedLanguagesAsHandEnteredAndRecalculatesTheRest()
    {
        var builder=Herohold.TestSupport.TestStores.Builder;var raw=Raw();var before=raw.ToJsonString();
        var draft=builder.Import(raw);var calculated=builder.Calculate(draft,null,false);
        Assert.IsTrue(calculated.Languages.Contains("Thieves' Cant"));CollectionAssert.AreEqual(new[]{"Infernal"},draft.Build.ManualLanguages);Assert.AreEqual(before,raw.ToJsonString());
        var changed=builder.Calculate(draft with{Build=calculated.Build with{ClassName="Fighter",HpHistory=null,OriginLanguages=["Giant","Orc"]}},null,false);
        CollectionAssert.AreEquivalent(new[]{"Common","Giant","Orc","Infernal"},changed.Languages);
        CollectionAssert.AreEqual(changed.Languages,builder.Calculate(draft with{Build=changed.Build},null,false).Languages,"Recalculating changes nothing more.");
    }
    [TestMethod]public void EditingASavedCharacterKeepsItsHandEnteredLanguages()
    {
        var rules=Herohold.TestSupport.TestStores.Workflow;var saved=rules.Adopt(rules.ImportJson(NativeCharacterBuilderTests.Fighter()).Draft);
        saved=saved with{Build=saved.Build! with{ManualLanguages=["Celestial"]}};
        var edited=rules.Adopt(rules.Edit(CharacterDraft.Of(saved),["name"],ChoiceValue.Of("Polyglot"),saved).Draft,saved);
        Assert.IsTrue(edited.Derived.Languages.Contains("Celestial"));CollectionAssert.AreEqual(new[]{"Celestial"},edited.Build!.ManualLanguages);
    }
}
