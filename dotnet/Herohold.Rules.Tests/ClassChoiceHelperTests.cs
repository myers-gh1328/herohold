using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class ClassChoiceHelperTests
{
    [TestMethod] public void DamageCapabilitiesUseStructuredFieldsWithoutDescriptionParsing()
    {
        var catalog=SpellCapabilityCatalog.Parse("""
            {"spell":[{"name":"Mind Sliver","level":0,"damageInflict":["psychic"]},{"name":"Guidance","level":0,"entries":["Deal 999 damage"],"scalingLevelDice":{"label":"d4"}},{"name":"Fireball","level":3,"damageInflict":["fire"]}]}
            """);
        Assert.IsTrue(catalog.CanDealDamage("Mind Sliver"));Assert.IsFalse(catalog.CanDealDamage("Guidance"));Assert.IsTrue(catalog.CanDealDamage("Fireball"));Assert.IsFalse(catalog.CanDealDamage("Absent"));
    }
    [TestMethod] public void WildShapeFiltersStructuredCrFlyingAndTypeWithStableOrdering()
    {
        var catalog=BeastChoiceCatalog.Parse("""
            [{"name":"Zebra","creature_type":"Beast","challenge_rating":0.25,"speeds":{"walk":40}},{"name":"Antelope","creature_type":"Beast","challenge_rating":0.25,"speeds":{"walk":40}},{"name":"Bird","creature_type":"Beast","challenge_rating":1,"speeds":{"fly":60}},{"name":"Bear","creature_type":"Beast","challenge_rating":1,"speeds":{"walk":40}},{"name":"Fiend","creature_type":"Fiend","challenge_rating":0,"speeds":{}}]
            """);
        CollectionAssert.AreEqual(new[]{"Antelope","Zebra"},catalog.WildShapeOptions(2,false).ToArray());
        CollectionAssert.AreEqual(new[]{"Antelope","Zebra","Bear"},catalog.WildShapeOptions(3,true).ToArray());
        CollectionAssert.AreEqual(new[]{"Antelope","Zebra","Bear","Bird"},catalog.WildShapeOptions(8,false).ToArray());
        Assert.AreEqual(0,catalog.WildShapeOptions(1,false).Count);
    }
    [TestMethod] public void HelperCatalogsRejectMalformedSourceShapesAndBounds()
    {
        Assert.Throws<ArgumentException>(()=>SpellCapabilityCatalog.Parse("[]"));
        Assert.Throws<ArgumentException>(()=>BeastChoiceCatalog.Parse("{}"));
        Assert.Throws<ArgumentException>(()=>BeastChoiceCatalog.Parse("[{\"name\":\"Bad\",\"creature_type\":\"Beast\",\"challenge_rating\":-1}]"));
    }
}
