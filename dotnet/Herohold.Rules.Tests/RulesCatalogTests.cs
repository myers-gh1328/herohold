using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class RulesCatalogTests
{
    private const string Record="[{\"name\":\"Fixture\"}]";
    private static RulesCatalogSnapshot Parse(string classes=Record,string spells=Record,string equipment="[\"Rope\",\"Lantern\"]",string raw="{\"spell\":[]}")
        =>RulesCatalogSnapshot.Parse(classes,Record,Record,Record,spells,equipment,Record,raw);
    /// <summary>The first class record as the cleaned catalog holds it.</summary>
    private static JsonNode Class(RulesCatalogSnapshot snapshot)=>snapshot.ToEnvelope()["catalogs"]!["classes"]![0]!;
    [TestMethod] public void ExactSevenKeyEnvelopePreservesArrayOrderAndEquipmentStrings()
    {
        var snapshot=Parse(classes:"[{\"name\":\"Second\"},{\"name\":\"First\"}]");
        var catalogs=snapshot.ToEnvelope()["catalogs"]!.AsObject();
        CollectionAssert.AreEquivalent(new[]{"classes","species","backgrounds","feats","spells","equipment","weapons"},catalogs.Select(pair=>pair.Key).ToArray());
        Assert.AreEqual("Second",snapshot.ClassRecords[0].Name);
        CollectionAssert.AreEqual(new[]{"Rope","Lantern"},snapshot.Equipment.ToArray());
        Assert.IsFalse(snapshot.SpellRecords[0].Ritual);
    }
    [TestMethod] public void RecursiveCleanupPreservesUnknownFieldsTypesAndUntaggedWhitespace()
    {
        var snapshot=Parse(classes:"""
            [{"name":"Fixture","unknown":{"tagged":"  {@DC 13|unused} {@Filter Light|Items|x} {@spell Fire Bolt|XPHB|Display}  ","raw":"  Keep whitespace  ","nested":[true,12,null,{"{@key untouched}":"{@item Rope||Visible|ignored}"}]}}]
            """);
        var extra=Class(snapshot)["unknown"]!;
        Assert.AreEqual("DC 13 Light Display",extra["tagged"]!.GetValue<string>());
        Assert.AreEqual("  Keep whitespace  ",extra["raw"]!.GetValue<string>());
        Assert.IsTrue(extra["nested"]![0]!.GetValue<bool>());Assert.AreEqual(12,extra["nested"]![1]!.GetValue<int>());Assert.IsNull(extra["nested"]![2]);
        Assert.AreEqual("ignored",extra["nested"]![3]!["{@key untouched}"]!.GetValue<string>());
    }
    [TestMethod] public void TagCleanupIsSinglePassAndUsesPythonWhitespaceAndNonemptyPipeParts()
    {
        var snapshot=Parse(classes:"""
            [{"name":"Fixture","nested":"\u001c {@outer {@spell Inner|XPHB|Visible}} \u001f","empty":"{@name}","unmatched":"  {@unclosed  "}]
            """);
        Assert.AreEqual("{@outer Visible}",Class(snapshot)["nested"]!.GetValue<string>());
        Assert.AreEqual("",Class(snapshot)["empty"]!.GetValue<string>());
        Assert.AreEqual("{@unclosed",Class(snapshot)["unmatched"]!.GetValue<string>());
    }
    [TestMethod] public void RitualEnrichmentUsesNameSourceDefaultsLastMatchAndPythonTruthiness()
    {
        var snapshot=Parse(spells:"""
            [{"name":"Detect Magic","ritual":false},{"name":"Detect Magic","source":"OTHER"},{"name":"Absent","source":"XPHB","ritual":true}]
            """,raw:"""
            {"spell":[{"name":"detect magic","source":"xphb","meta":{"ritual":false}},{"name":"DETECT MAGIC","source":"XPHB","meta":{"ritual":"yes"}}]}
            """);
        Assert.IsTrue(snapshot.SpellRecords[0].Ritual);Assert.IsFalse(snapshot.SpellRecords[1].Ritual);Assert.IsFalse(snapshot.SpellRecords[2].Ritual);
    }
    [TestMethod] public void SpellComponentsRetainExactMaterialCostAndConsumptionFromTheMatchingEdition()
    {
        var snapshot=Parse(spells:"""[{"name":"Fixture","source":"XPHB","components":["V","S","M"]},{"name":"Fixture","source":"OTHER","components":["V"]}]""",raw:"""
            {"spell":[{"name":"Fixture","source":"XPHB","components":{"v":true,"s":true,"m":{"text":"a gem worth 50 GP","cost":5000,"consume":true}}}]}
            """);
        var spell=snapshot.SpellRecords[0];
        Assert.AreEqual("a gem worth 50 GP",spell.MaterialDescription,"Material details must survive the trusted raw catalog boundary.");Assert.AreEqual(5000,spell.MaterialCostCp);Assert.IsTrue(spell.MaterialConsumed);
        Assert.AreEqual("",snapshot.SpellRecords[1].MaterialDescription,"A different edition's identically named record must not acquire these components.");Assert.IsNull(snapshot.SpellRecords[1].MaterialConsumed);
    }
    [TestMethod] public void EveryPropertyAndEnvelopeReadIsDeeplyDetached()
    {
        var snapshot=Parse(classes:"[{\"name\":\"Fixture\",\"nested\":{\"value\":7}}]");
        Class(snapshot)["nested"]!["value"]=0;
        snapshot.ToEnvelope()["catalogs"]!["equipment"]!.AsArray().Clear();
        Assert.AreEqual(7,Class(snapshot)["nested"]!["value"]!.GetValue<int>());Assert.AreEqual(2,snapshot.Equipment.Count);
    }
    [TestMethod] public void MalformedShapesAndExcessiveDepthFailVisibly()
    {
        Assert.Throws<ArgumentException>(()=>Parse(classes:"{}"));
        Assert.Throws<ArgumentException>(()=>Parse(equipment:"[1]"));
        Assert.Throws<ArgumentException>(()=>Parse(raw:"[]"));
        Assert.Throws<ArgumentException>(()=>Parse(classes:"[{\"name\":\"Depth\",\"nested\":"+new string('[',70)+"0"+new string(']',70)+"}]"));
    }
    [TestMethod] public void ByteRecordAndNodeBudgetsRejectBeforeUnboundedCatalogUse()
    {
        Assert.Throws<ArgumentException>(()=>Parse(classes:new string('x',16*1024*1024+1)));
        var shared="[{\"name\":\"Fixture\",\"text\":\""+new string('x',4*1024*1024)+"\"}]";
        Assert.Throws<ArgumentException>(()=>RulesCatalogSnapshot.Parse(shared,shared,shared,shared,shared,shared,shared,shared));
        Assert.Throws<ArgumentException>(()=>Parse(classes:"["+string.Join(',',Enumerable.Repeat("{}",50001))+"]"));
        Assert.Throws<ArgumentException>(()=>Parse(classes:"[{\"name\":\"Fixture\",\"many\":["+string.Join(',',Enumerable.Repeat("0",250001))+"]}]"));
    }
}
