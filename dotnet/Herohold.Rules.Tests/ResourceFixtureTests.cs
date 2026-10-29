using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class ResourceFixtureTests
{
    [TestMethod]public void AllTwelveClassesAndAllSubclassesMatchFrozenResourceReferences()
    {
        var root=SpellGrantFixture.Root();var classes=ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes"));var rules=new ClassResourceRules(classes);
        var fixtures=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"dotnet/fixtures/native_resources.json")))!["cases"]!.AsArray();
        var errors=new List<string>();
        foreach(var fixture in fixtures.OfType<JsonObject>())
        {
            var raw=fixture["canonical"]!.DeepClone().AsObject();var before=raw.ToJsonString();
            var build=new NativeBuildSummary{ClassName=raw["class"]!.GetValue<string>(),Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>new Calculation(raw["ability_modifiers"]![ability.ToString()]!.GetValue<int>(),[]))}};
            var features=new ActiveFeatureSummary{Features=raw["features"]!.AsArray().Select(node=>node!.GetValue<string>()).ToArray()};
            var result=rules.Derive(raw,build,features);
            var metadata=JsonSerializer.SerializeToNode(result.Metadata,new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsArray();
            var expectedMetadata=fixture["expectedMetadata"]!.AsArray();
            var derived=JsonSerializer.SerializeToNode(result.DerivedResources);if(!JsonNode.DeepEquals(fixture["expectedDerived"],derived))errors.Add(fixture["id"]+": derived "+Difference(fixture["expectedDerived"],derived));
            foreach(var expected in expectedMetadata.OfType<JsonObject>())
            {var actual=metadata.OfType<JsonObject>().FirstOrDefault(pool=>pool["id"]!.GetValue<string>()==expected["id"]!.GetValue<string>());if(!JsonNode.DeepEquals(expected,actual))errors.Add(fixture["id"]+": pool "+expected["id"]+" "+Difference(expected,actual));}
            if(metadata.Count!=expectedMetadata.Count)errors.Add(fixture["id"]+": pool count "+metadata.Count+" vs "+expectedMetadata.Count);
            Assert.AreEqual(before,raw.ToJsonString(),"Resource calculation must leave input unchanged.");
        }
        Assert.AreEqual(204,fixtures.Count);Assert.AreEqual(0,errors.Count,string.Join("\n",errors.Take(25)));
    }
    private static string Difference(JsonNode? expected,JsonNode? actual,string path="")
    {
        if(expected is JsonObject obj&&actual is JsonObject other)
            foreach(var key in obj.Select(pair=>pair.Key).Concat(other.Select(pair=>pair.Key)).Distinct())if(!JsonNode.DeepEquals(obj[key],other[key]))return Difference(obj[key],other[key],path+"."+key);
        return path+" expected="+expected?.ToJsonString()+" actual="+actual?.ToJsonString();
    }
}
