using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class WeaponCapabilityTests
{
    private const string Empty="[]";
    private static WeaponCapabilityCatalog Catalog()
    {
        var snapshot=RulesCatalogSnapshot.Parse(Empty,Empty,Empty,Empty,Empty,Empty,"""
            [{"name":"Rapier","category":"Martial","mastery":"Vex"},{"name":"Shortsword","category":"Martial","mastery":"Vex"},{"name":"Whip","category":"Martial","mastery":"Slow"},{"name":"Dagger","category":"Simple","mastery":"Nick"}]
            ""","{\"spell\":[]}");
        return WeaponCapabilityCatalog.Parse("""
            {"baseitem":[{"name":"Rapier","source":"XPHB","weaponCategory":"martial","property":["F|XPHB"]},{"name":"Shortsword","source":"XPHB","weaponCategory":"martial","property":["F|XPHB","L|XPHB"]},{"name":"Whip","source":"XPHB","weaponCategory":"martial","property":["F|XPHB","R|XPHB"]},{"name":"Dagger","source":"XPHB","weaponCategory":"simple","property":["F|XPHB","L|XPHB"]}]}
            """,snapshot);
    }
    [TestMethod] public void MonkLightPermissionExcludesRapierAndWhipUsingStructuredProperties()
    {
        CollectionAssert.AreEqual(new[]{"Shortsword","Dagger"},Catalog().EligibleMasteries([new(WeaponProficiencyScope.Simple),new(WeaponProficiencyScope.MartialLight)]).ToArray());
        CollectionAssert.AreEqual(new[]{"Rapier","Shortsword","Whip","Dagger"},Catalog().EligibleMasteries([new(WeaponProficiencyScope.Simple),new(WeaponProficiencyScope.MartialFinesseOrLight)]).ToArray());
    }
    [TestMethod] public void NamedAndMartialGrantsPreserveCatalogOrderAndDeduplicate()
    {
        CollectionAssert.AreEqual(new[]{"Rapier","Shortsword","Whip"},Catalog().EligibleMasteries([new(WeaponProficiencyScope.Martial),new(WeaponProficiencyScope.Named,"Rapier")]).ToArray());
        Assert.AreEqual(0,Catalog().EligibleMasteries([new(WeaponProficiencyScope.Named,"Unknown")]).Count);
        Assert.Throws<ArgumentException>(()=>Catalog().EligibleMasteries([new((WeaponProficiencyScope)999)]));
    }
    [TestCategory("RulesData")][TestMethod] public void BarbarianClassMasteryIsMeleeOnlyWhileTheGeneralWeaponMasterFeatRemainsUnrestricted()
    {
        var catalog=WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot());
        WeaponProficiencyRule[] rules=[new(WeaponProficiencyScope.Simple),new(WeaponProficiencyScope.Martial)];
        Assert.IsTrue(catalog.EligibleMasteries(rules).Contains("Longbow"));
        var barbarian=catalog.EligibleClassMasteries("Barbarian",rules);Assert.IsTrue(barbarian.Contains("Longsword"));Assert.IsFalse(barbarian.Contains("Longbow"));Assert.IsFalse(barbarian.Contains("Dart"));
        Assert.IsTrue(catalog.EligibleClassMasteries("Fighter",rules).Contains("Longbow"));
    }
}
