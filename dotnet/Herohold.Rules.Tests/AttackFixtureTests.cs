using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class AttackFixtureTests
{
    [TestMethod]public void AllTrustedWeaponProfilesMatchFrozenBaseAndArcheryReferences()
    {
        var root=SpellGrantFixture.Root();var rules=new AttackActionRules(WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(root,"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot()));
        var fixtures=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"dotnet/fixtures/native_attack_actions.json")))!["cases"]!.AsArray();
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};var errors=new List<string>();
        foreach(var fixture in fixtures.OfType<JsonObject>())
        {
            var raw=fixture["canonical"]!.DeepClone().AsObject();var build=new NativeBuildSummary{ClassName="Fighter",Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>new Calculation(raw["ability_modifiers"]![ability.ToString()]!.GetValue<int>(),[]))}};
            var features=new ActiveFeatureSummary{Proficiencies=[new("class:Fighter",ProficiencySourceKind.Weapon,"Simple Weapons"),new("class:Fighter",ProficiencySourceKind.Weapon,"Martial Weapons")]};
            var items=raw["equipment"]!.AsArray().Select((node,index)=>new EquipmentItem{Id="fixture-"+index,Name=node!.GetValue<string>(),Equipped=true}).ToArray();
            var result=rules.Derive(raw,build,features,new(),items);
            Assert.AreEqual(1,result.Attacks.Count(a=>a.Kind=="unarmed"),"The ordinary unarmed action is independent of the equipped weapon profile.");
            Assert.IsTrue(result.Attacks.All(a=>a.Kind is "weapon" or "unarmed"));
            var weapons=result.Attacks.Where(a=>a.Kind=="weapon").ToArray();
            Assert.IsTrue(weapons.All(a=>a.FixedDamage is null),"Frozen weapon profiles retain their original dice-based damage contract.");
            var actual=JsonSerializer.SerializeToNode(weapons,json)!.AsArray();
            foreach(var item in actual.OfType<JsonObject>()){item.Remove("notes");item.Remove("fixedDamage");}
            if(!JsonNode.DeepEquals(fixture["expectedAttacks"],actual))errors.Add(fixture["id"]+": expected="+fixture["expectedAttacks"]!.ToJsonString()+" actual="+actual.ToJsonString());
        }
        Assert.AreEqual(82,fixtures.Count);Assert.AreEqual(0,errors.Count,string.Join("\n",errors.Take(10)));
    }
}
