using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class BuildFactOwnershipTests
{
    private static Dictionary<string,IReadOnlyList<string>> Derived()=>new(){["damage_resistances"]=new[]{"Necrotic","Radiant"},["weapon_proficiencies"]=new[]{"Simple Weapons","Martial Weapons"}};
    [TestMethod]public void FirstImportRetainsUnexplainedFactsAndSeparatesThemFromActualSpeciesAndClassFacts()
    {
        var raw=new JsonObject{["tool_proficiencies"]=new JsonArray("Herbalism Kit"),["damage_resistances"]=new JsonArray("Cold","Necrotic","Radiant"),["condition_immunities"]=new JsonArray("Frightened")};var before=raw.ToJsonString();
        var result=BuildFactOwnershipRules.Derive(raw,Derived());
        CollectionAssert.AreEquivalent(new[]{"Cold","Necrotic","Radiant"},result.Values["damage_resistances"].ToArray());CollectionAssert.AreEqual(new[]{"Cold"},result.Manual["damage_resistances"].ToArray());
        Assert.AreEqual("Herbalism Kit",result.Manual["tool_proficiencies"].Single());Assert.AreEqual("Frightened",result.Manual["condition_immunities"].Single());Assert.IsTrue(result.Warnings.Count>0);Assert.AreEqual(before,raw.ToJsonString());
    }
    [TestMethod]public void RemovedKnownSourcesDisappearWhileManualFactsSurviveRepeatedRecalculationAndDetachedSidecars()
    {
        var raw=new JsonObject{["damage_resistances"]=new JsonArray("Cold")};var first=BuildFactOwnershipRules.Derive(raw,Derived());raw["folio_build_fact_sources"]=first.ToSidecar();raw["damage_resistances"]=new JsonArray(first.Values["damage_resistances"].Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray());
        var next=BuildFactOwnershipRules.Derive(raw,new Dictionary<string,IReadOnlyList<string>>());CollectionAssert.AreEqual(new[]{"Cold"},next.Values["damage_resistances"].ToArray());Assert.AreEqual(0,next.Values["weapon_proficiencies"].Count);
        var sidecar=next.ToSidecar();sidecar["manual"]!["damage_resistances"]!.AsArray().Clear();Assert.AreEqual("Cold",next.Manual["damage_resistances"].Single());
        raw["folio_build_fact_sources"]=next.ToSidecar();raw["damage_resistances"]=new JsonArray("Cold");Assert.IsTrue(JsonNode.DeepEquals(next.ToSidecar(),BuildFactOwnershipRules.Derive(raw,new Dictionary<string,IReadOnlyList<string>>()).ToSidecar()));
    }
    [TestMethod]public void ClosedBoundedOwnershipRejectsMalformedUnknownFieldsAndOversizedOrWrongTypedFacts()
    {
        foreach(var raw in new[]{new JsonObject{["tool_proficiencies"]=new JsonArray(1)},new JsonObject{["damage_resistances"]=new JsonArray(new string('x',201))},new JsonObject{["folio_build_fact_sources"]=new JsonObject{["manual"]=new JsonObject{["unknown"]=new JsonArray()}}},new JsonObject{["folio_build_fact_sources"]=new JsonObject{["derived"]=new JsonObject{["tool_proficiencies"]="Herbalism Kit"}}}})
            Assert.Throws<ArgumentException>(()=>BuildFactOwnershipRules.Derive(raw,Derived()));
        Assert.Throws<ArgumentException>(()=>BuildFactOwnershipRules.Derive(new JsonObject(),new Dictionary<string,IReadOnlyList<string>>{["unknown"]=new[]{"Cold"}}));
    }
    [TestMethod]public void RecordedHandEnteredFactsMatchTheDocumentThatCarriedThem()
    {
        var manual=new Herohold.Core.ManualBuildFacts{DamageResistances=["Cold"],ToolProficiencies=["Herbalism Kit"]};
        var document=new JsonObject{["folio_build_fact_sources"]=new JsonObject{["manual"]=System.Text.Json.JsonSerializer.SerializeToNode(manual)}};
        var typed=BuildFactOwnershipRules.Derive(ShownOwnership.Recorded(manual),Derived());var read=BuildFactOwnershipRules.Derive(document,Derived());
        Assert.IsTrue(JsonNode.DeepEquals(read.ToSidecar(),typed.ToSidecar()),typed.ToSidecar().ToJsonString());
        CollectionAssert.AreEqual(new[]{"Cold"},typed.Manual["damage_resistances"].ToArray());
        CollectionAssert.AreEquivalent(new[]{"Cold","Necrotic","Radiant"},typed.Values["damage_resistances"].ToArray());
    }
}
