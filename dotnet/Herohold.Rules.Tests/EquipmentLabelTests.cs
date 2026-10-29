using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class EquipmentLabelTests
{
    [TestCategory("RulesData")][TestMethod]public void ActualStartingPackagesResolveOnlyTrustedQuantitySuffixes()
    {
        var catalog=SpellGrantFixture.Snapshot();var names=catalog.Equipment.Concat(catalog.WeaponRecords.Select(weapon=>weapon.Name)).ToArray();
        foreach(var (label,name,quantity) in new[]{("Dagger x2","Dagger",2),("Dagger x5","Dagger",5),("Handaxe x4","Handaxe",4),("Javelin x8","Javelin",8)})
        {var result=EquipmentLabelRules.Normalize(label,names);Assert.AreEqual(name,result.Name);Assert.AreEqual(quantity,result.Quantity);}
        Assert.AreEqual(new EquipmentQuantityLabel("Synthetic Sword x2",1),EquipmentLabelRules.Normalize("Synthetic Sword x2",names));
        Assert.AreEqual(new EquipmentQuantityLabel("Arrows (20)",1),EquipmentLabelRules.Normalize("Arrows (20)",names));
    }
    [TestMethod]public void ExactNamesWinAndRecognizedQuantitiesHaveClosedBounds()
    {
        string[] names=["Dagger","Dagger x2"];
        Assert.AreEqual(new EquipmentQuantityLabel("Dagger x2",1),EquipmentLabelRules.Normalize("Dagger x2",names));
        Assert.AreEqual(new EquipmentQuantityLabel("Dagger",100000),EquipmentLabelRules.Normalize("Dagger x100000",names));
        foreach(var label in new[]{"Dagger x0","Dagger x100001","Dagger x999999999999999999999"})Assert.ThrowsExactly<ArgumentException>(()=>EquipmentLabelRules.Normalize(label,names));
        Assert.AreEqual(new EquipmentQuantityLabel("Custom x0",1),EquipmentLabelRules.Normalize("Custom x0",names));
    }
}
