using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class LanguageTests
{
    [TestMethod]public void OriginLanguagesAreTwoStandardChoicesAndCommonIsAutomatic()
    {
        var raw=JsonNode.Parse("""{"origin_languages":["Elvish","Dwarvish"],"languages":["Infernal","Fictional dialect"]}""")!.AsObject();var before=raw.ToJsonString();var result=CanonicalFixtures.Languages(raw,[]);
        CollectionAssert.AreEqual(new[]{"Common","Elvish","Dwarvish","Infernal","Fictional dialect"},result.Languages.ToArray());Assert.AreEqual(9,LanguageRules.StandardLanguages.Count);Assert.IsFalse(LanguageRules.StandardLanguages.Contains("Infernal"));Assert.IsFalse(LanguageRules.StandardLanguages.Contains("Common"));Assert.AreEqual(before,raw.ToJsonString());
        Assert.IsTrue(result.Sources.Any(source=>source.SourceId=="origin:common"&&source.Target=="Common"));Assert.IsTrue(result.Sources.Any(source=>source.SourceId=="origin:languages"&&source.Target=="Elvish"));Assert.IsTrue(result.Sources.Any(source=>source.SourceId=="manual:languages"&&source.Target=="Infernal"));
    }
    [TestMethod]public void KnownRemovedLanguageSourcesDisappearWhileExplicitManualLanguagesSurvive()
    {
        var raw=JsonNode.Parse("""{"origin_languages":["Giant","Orc"],"languages":["Common","Elvish","Dwarvish","Druidic","Infernal"],"folio_language_sources":{"manual":["Infernal"],"derived":["Common","Elvish","Dwarvish","Druidic"]}}""")!.AsObject();
        var result=CanonicalFixtures.Languages(raw,[]);CollectionAssert.AreEqual(new[]{"Common","Giant","Orc","Infernal"},result.Languages.ToArray());
        raw["folio_language_sources"]!["manual"]!.AsArray().Add("Druidic");Assert.IsTrue(CanonicalFixtures.Languages(raw,[]).Languages.Contains("Druidic"));
    }
    [TestMethod]public void MissingLegacyChoicesStayUnresolvedWithoutInventingLanguagesAndInvalidDraftsStayRepairable()
    {
        var missing=CanonicalFixtures.Languages(new(),[]);CollectionAssert.AreEqual(new[]{"Common"},missing.Languages.ToArray());Assert.IsTrue(missing.Issues.Count>0);
        var raw=JsonNode.Parse("""{"origin_languages":["Elvish","Elvish","Infernal"]}""")!.AsObject();var before=raw.ToJsonString();Assert.ThrowsExactly<ArgumentException>(()=>CanonicalFixtures.Languages(raw,[]));
        var draft=CanonicalFixtures.Languages(raw,[],true);CollectionAssert.AreEqual(new[]{"Common","Elvish"},draft.Languages.ToArray());Assert.IsTrue(draft.Issues.Count>0);Assert.AreEqual(before,raw.ToJsonString());
    }
    [TestMethod]public void SourceSidecarIsStrictBoundedAndRareGrantedSourcesRemainLegitimate()
    {
        foreach(var invalid in new[]{"{\"folio_language_sources\":{\"unknown\":[]}}","{\"folio_language_sources\":{\"manual\":[17]}}"})Assert.ThrowsExactly<ArgumentException>(()=>CanonicalFixtures.Languages(JsonNode.Parse(invalid)!.AsObject(),[]));
        var many=new JsonArray(Enumerable.Range(0,101).Select(index=>(JsonNode?)JsonValue.Create("Language "+index)).ToArray());Assert.ThrowsExactly<ArgumentException>(()=>CanonicalFixtures.Languages(new(){["languages"]=many},[]));
        var result=CanonicalFixtures.Languages(new(){["origin_languages"]=new JsonArray("Giant","Orc")},[new("background:fictional",ProficiencySourceKind.Language,"Celestial")]);Assert.IsTrue(result.Languages.Contains("Celestial"));
    }
    [TestCategory("RulesData")][TestMethod]public void ClassLanguageFactsAreSourceOwnedAndIncludeRogueAndDruidSecrets()
    {
        var catalog=CharacterBuildCatalog.Parse(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Read("backgrounds"),SpellGrantFixture.Read("species"));
        foreach(var (name,language) in new[]{("Rogue","Thieves' Cant"),("Druid","Druidic")})
        {
            var raw=new JsonObject{["class"]=name,["species"]="Human",["background"]="Soldier",["level"]=1,["base_ability_scores"]=new JsonObject(Enum.GetNames<Herohold.Core.Ability>().Select(ability=>new KeyValuePair<string,JsonNode?>(ability,JsonValue.Create(10)))),["origin_languages"]=new JsonArray("Elvish","Dwarvish")};
            var summary=catalog.Derive(raw,[],true);Assert.IsTrue(summary.Languages.Languages.Contains(language));Assert.IsTrue(summary.ProficiencySources.Any(source=>source.Kind==ProficiencySourceKind.Language&&source.Target==language));
        }
    }
}
