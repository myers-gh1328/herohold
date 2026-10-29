using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class BuildFactsTests
{
    [TestMethod] public void ReplaceInKeepsWhatTheLastCalculationDerivedAndReplacesOnlyTheHandEnteredLists()
    {
        var document=JsonNode.Parse("""
            {"class":"Ranger","level":5,
             "folio_native_build_sources":{"skills":["Stealth"],"expertise":[],"saves":[],"derivedSkills":["Athletics"],"derivedExpertise":["Athletics"],"derivedSaves":["Strength"]},
             "folio_language_sources":{"manual":["Elvish"],"derived":["Common"]},
             "folio_build_fact_sources":{"manual":{"damage_resistances":["Fire"]},"derived":{"damage_resistances":["Poison"]}}}
            """)!.AsObject();
        var build=BuildFacts.FromCanonical(document) with{ManualSkills=["Insight"],ManualLanguages=["Dwarvish"]};
        var replaced=build.ReplaceIn(document,"probe");
        var native=replaced["folio_native_build_sources"]!;
        CollectionAssert.AreEqual(new[]{"Insight"},native["skills"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        CollectionAssert.AreEqual(new[]{"Athletics"},native["derivedExpertise"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        CollectionAssert.AreEqual(new[]{"Strength"},native["derivedSaves"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        Assert.AreEqual("Dwarvish",replaced["folio_language_sources"]!["manual"]![0]!.GetValue<string>());Assert.AreEqual("Common",replaced["folio_language_sources"]!["derived"]![0]!.GetValue<string>());
        Assert.AreEqual("Poison",replaced["folio_build_fact_sources"]!["derived"]!["damage_resistances"]![0]!.GetValue<string>());
        var cleared=(build with{ManualSkills=null,ManualExpertise=null,ManualSaves=null}).ReplaceIn(document,"probe");
        Assert.IsNull(cleared["folio_native_build_sources"]!["skills"],"A hand-entered list the build no longer has is removed.");
        Assert.IsNotNull(cleared["folio_native_build_sources"]!["derivedSkills"]);
    }
}
