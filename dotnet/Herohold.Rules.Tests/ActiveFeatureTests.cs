using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class ActiveFeatureTests
{
    private const string DefaultClasses="""[{"name":"Fighter","hit_die":10,"level_1_features":["Second Wind"],"progression":{"1":{"features":[]}},"subclasses":[]}]""";
    private static ActiveFeatureRules Rules(string cls=DefaultClasses)
    {
        var catalog=RulesCatalogSnapshot.Parse(cls,"""[{"name":"Aasimar","size":"Small or Medium","traits":["Celestial Resistance"]},{"name":"Tiefling","traits":["Fiendish Legacy"],"lineage_options":[{"name":"Abyssal"}]},{"name":"Dragonborn","traits":["Draconic Ancestry"],"choice_options":{"key":"draconic_ancestry","options":["Black","Red"]}}]""","[]","[]","[]","[]","[]","{}");
        var classes=ClassProgressionCatalog.Parse(cls);var classChoices=new ClassChoiceRules(classes,catalog,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}"));
        return new(catalog,classes,classChoices,RulesReference.Empty,SpeciesSourceCatalog.Parse("""{"race":[{"name":"Aasimar","source":"XPHB","darkvision":60,"resist":["necrotic","radiant"]},{"name":"Tiefling","source":"XPHB","darkvision":60,"resist":[{"choose":{"from":["poison","necrotic","fire"]}}],"_versions":[{"name":"Tiefling; Abyssal Legacy","darkvision":120,"resist":["poison"]}]},{"name":"Dragonborn","source":"XPHB","darkvision":60,"resist":[{"choose":{"from":["acid","fire"]}}]}]}"""));
    }
    private static JsonObject Character(string species)=>new(){["class"]="Fighter",["level"]=1,["species"]=species,["class_options"]=new JsonObject(),["species_options"]=new JsonObject()};
    private static NativeBuildSummary Build()=>new(){ClassName="Fighter"};
    [TestMethod] public void StructuredSpeciesDefensesAndSensesAreVisibleWithTheirSource()
    {
        var result=Rules().Derive(Character("Aasimar"),Build());CollectionAssert.AreEquivalent(new[]{"Necrotic","Radiant"},result.Defenses.Select(d=>d.Type).ToArray());Assert.AreEqual(60,result.Senses.Single(s=>s.Type=="Darkvision").Range);Assert.IsTrue(result.Features.Contains("Second Wind"));
        Assert.IsTrue(result.Defenses.All(d=>d.Source.Contains("Aasimar")));Assert.AreEqual("Small or Medium",result.Size);
    }
    [TestMethod] public void SpeciesVariantOverridesDoNotGrantEveryAlternativeResistance()
    {
        var c=Character("Tiefling");c["species_options"]!["lineage"]="Abyssal";var result=Rules().Derive(c,Build());CollectionAssert.AreEqual(new[]{"Poison"},result.Defenses.Select(d=>d.Type).ToArray());Assert.AreEqual(120,result.Senses.Single().Range);
        c["species_options"]!["lineage"]="A";Assert.AreEqual(0,Rules().Derive(c,Build()).Defenses.Count);
    }
    [TestMethod] public void DragonbornResistanceRequiresAnEligibleAncestry()
    {
        var c=Character("Dragonborn");Assert.AreEqual(0,Rules().Derive(c,Build()).Defenses.Count);c["species_options"]!["draconic_ancestry"]="Red";Assert.AreEqual("Fire",Rules().Derive(c,Build()).Defenses.Single().Type);
        c["species_options"]!["draconic_ancestry"]="Gold";Assert.AreEqual(0,Rules().Derive(c,Build()).Defenses.Count);
    }
    [TestMethod] public void InspectionIsPureAndListsNoConfigurationsWithoutConfigurableFeatures()
    {
        var c=Character("Aasimar");var before=c.ToJsonString();var result=Rules().Derive(c,Build());Assert.AreEqual(before,c.ToJsonString());Assert.AreEqual(0,result.FeatureConfigurations.Count);
    }
    [TestMethod] public void HunterOptionsCanChangeAfterEitherShortOrLongRest()
    {
        const string classes="""[{"name":"Ranger","hit_die":10,"progression":{"1":{"features":[]}},"subclasses":[{"name":"Hunter","short_name":"Hunter","subclass_options":{"hunters_prey":{"from":["Colossus Slayer","Horde Breaker"],"choose":1}}}]}]""";
        var c=Character("Aasimar");c["class"]="Ranger";c["subclass"]="Hunter";c["level"]=7;c["class_options"]!["hunters_prey"]="Colossus Slayer";
        var result=Rules(classes).Derive(c,new(){ClassName="Ranger",SubclassName="Hunter"});
        var prey=result.FeatureConfigurations.Single(r=>r.FeatureId=="hunters_prey");Assert.AreEqual("short_or_long_rest",prey.ChangeTiming);
        CollectionAssert.AreEqual(new[]{"Colossus Slayer","Horde Breaker"},prey.EligibleOptions.ToArray());Assert.AreEqual(("Colossus Slayer",3),(prey.InitialValue,prey.LevelGained));
        c["feature_configurations"]=new JsonArray(new JsonObject{["feature_id"]="hunters_prey",["current_value"]="Wrong"});
        var invalid=Rules(classes).Derive(c,new(){ClassName="Ranger",SubclassName="Hunter"});Assert.IsTrue(invalid.Issues.Count>0);Assert.AreEqual("Wrong",invalid.FeatureConfigurations.Single().CurrentValue);
    }
}
