using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class CompanionHealthTests
{
    [TestMethod][DataRow(10,5,-3,10,2)][DataRow(10,5,-8,7,0)][DataRow(10,5,-50,0,0)][DataRow(0,5,-3,0,2)][DataRow(7,5,3,10,5)]
    public void DamageAbsorbsTemporaryHpBeforeClampedCurrentHpAndHealingPreservesBuffer(int hp,int temp,int delta,int expectedHp,int expectedTemp)
    {
        var c=Fixtures.Ranger();c=AdventureRules.SetFacts(c,new(){Companions=[new(){Id="owl",Name="Owl",MaximumHp=10,CurrentHp=hp,TemporaryHp=temp}]});
        var changed=AdventureRules.AdjustCompanionHp(c,"owl",delta);var owl=changed.Sheet.Adventure.Companions[0];Assert.AreEqual(expectedHp,owl.CurrentHp);Assert.AreEqual(expectedTemp,owl.TemporaryHp);Assert.AreEqual(hp,c.Sheet.Adventure.Companions[0].CurrentHp);Assert.AreEqual(temp,c.Sheet.Adventure.Companions[0].TemporaryHp);
    }
}
