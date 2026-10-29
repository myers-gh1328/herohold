using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class AdventureSafetyTests
{
    [TestMethod]public void UncarriedItemsCannotRemainEquippedOrBeUsed()
    {
        var c=Fixtures.Ranger();var item=new EquipmentItem{Id="stored",Name="Stored potion",Carried=false,Equipped=false,Consumable=true,Quantity=1};c=c with{Equipment=[item]};
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(c,item.Id));
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(c with{Equipment=[item with{Equipped=true}]}));
    }
    [TestMethod]public void NullCompanionAndResourceEntriesBecomeControlledValidationErrors()
    {
        var c=Fixtures.Ranger();Assert.ThrowsExactly<ArgumentException>(()=>AdventureRules.SetFacts(c,new(){Companions=[null!]}));
        Assert.ThrowsExactly<ArgumentException>(()=>AdventureRules.SetFacts(c,new(){Companions=[new(){Id="owl",Name="Owl",Resources=[null!]}]}));
    }
    [TestMethod]public void AtomicCompanionCounterAdjustmentPreservesNewerIndependentFactsAndRejectsBounds()
    {
        var c=AdventureRules.SetFacts(Fixtures.Ranger(),new(){Movement=[new(){Kind="Fly",Speed=40}],Companions=[new(){Id="owl",Name="Owl",CurrentHp=3,MaximumHp=5,Resources=[new(){Id="ward",Name="Ward",Maximum=2,Remaining=2}]}]});
        var changed=AdventureRules.AdjustCompanionResource(c,"owl","ward",-1);Assert.AreEqual(1,changed.Sheet.Adventure.Companions[0].Resources[0].Remaining);Assert.AreEqual(3,changed.Sheet.Adventure.Companions[0].CurrentHp);Assert.AreEqual(40,changed.Sheet.Adventure.Movement[0].Speed);Assert.AreEqual(2,c.Sheet.Adventure.Companions[0].Resources[0].Remaining);
        Assert.ThrowsExactly<ArgumentException>(()=>AdventureRules.AdjustCompanionResource(c,"owl","ward",-3));
    }
}
