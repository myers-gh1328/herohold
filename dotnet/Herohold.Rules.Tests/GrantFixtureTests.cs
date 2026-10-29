using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class GrantFixtureTests
{
    [TestMethod]public void SubclassFeatOriginAndSpeciesGrantIdentitiesAndPoliciesMatchFrozenReferences()
    {
        var rules=new SpellGrantRules(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Snapshot(),SpellGrantFixture.Species());
        var fixtures=JsonNode.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"dotnet/fixtures/native_spell_grants.json")))!["cases"]!.AsArray();
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};var errors=new List<string>();
        foreach(var fixture in fixtures.OfType<JsonObject>())
        {
            var raw=fixture["canonical"]!.DeepClone().AsObject();var before=raw.ToJsonString();var modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>raw["ability_modifiers"]![ability.ToString()]!.GetValue<int>());
            var result=rules.Derive(raw,modifiers);var metadata=JsonSerializer.SerializeToNode(result.Metadata,json)!.AsArray();var expected=fixture["expectedMetadata"]!.AsArray();
            foreach(var grant in expected.OfType<JsonObject>())
            {
                var actual=metadata.OfType<JsonObject>().FirstOrDefault(record=>record["id"]!.GetValue<string>()==grant["id"]!.GetValue<string>());
                if(!JsonNode.DeepEquals(grant,actual))errors.Add(fixture["id"]+": "+grant["spellName"]+" "+grant["source"]+" expected="+grant.ToJsonString()+" actual="+actual?.ToJsonString());
            }
            if(expected.Count!=metadata.Count)errors.Add(fixture["id"]+": grant count "+metadata.Count+" vs "+expected.Count);
            Assert.AreEqual(before,raw.ToJsonString(),"Grant calculation must leave input unchanged.");
        }
        Assert.AreEqual(165,fixtures.Count);Assert.AreEqual(0,errors.Count,string.Join("\n",errors.Take(15)));
    }
}
