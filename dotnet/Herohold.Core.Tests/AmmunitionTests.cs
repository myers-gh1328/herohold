using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class AmmunitionTests
{
    private static WorkspaceSnapshot Workspace(CharacterState c)=>new(){Revision=1,Character=c};
    private static CharacterState Archer(bool counting,int arrows=2)=>Fixtures.Ranger() with
    {
        Equipment=[new(){Id="bow",Name="Longbow",Equipped=true},new(){Id="arrows",Name="Arrow",CatalogItemId="XPHB|Arrow",Quantity=arrows,Consumable=true},new(){Id="sword",Name="Shortsword",Equipped=true}],
        Derived=new(){Attacks=[new(){Id="weapon:longbow",Name="Longbow",Ammunition="Arrow"},new(){Id="weapon:shortsword",Name="Shortsword"}]},
        Sheet=new(){CountAmmunition=counting}
    };
    private static RollRequest Attack(string key,string id="shot")=>new(){OperationId=id,SourceRevision=1,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,Mode=DiceMode.Physical,PhysicalD20=[12],
        CombatContext=new(){ActionId="attack:"+key}};
    private static int Arrows(CharacterState c)=>c.Equipment.Single(i=>i.Id=="arrows").Quantity;

    [TestMethod]public void AttacksSpendNoAmmunitionUntilCountingIsOn()
    {
        var shot=PlayRules.Roll(Workspace(Archer(false)),Attack("longbow"));
        Assert.AreEqual(2,Arrows(shot));Assert.AreEqual(0,shot.Sheet.AmmunitionFired.Count);
    }

    [TestMethod]public void ACountedShotSpendsOneMatchingArrowAndRefusesWhenNoneAreLeft()
    {
        var c=Archer(true,1);
        var shot=PlayRules.Roll(Workspace(c),Attack("longbow"));
        Assert.AreEqual(0,Arrows(shot));Assert.AreEqual(1,shot.Sheet.AmmunitionFired["Arrow"]);Assert.AreEqual(0,AmmunitionRules.Carried(shot,"Arrow"));
        var error=Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Roll(Workspace(shot with{Play=shot.Play with{LastRoll=null}}),Attack("longbow","second")));
        StringAssert.Contains(error.Message,"No Arrows left");
        var stab=PlayRules.Roll(Workspace(c),Attack("shortsword","stab"));
        Assert.AreEqual(1,Arrows(stab),"A melee weapon fires no ammunition.");
    }

    [TestMethod]public void ArrowsStoredElsewhereCannotBeFired()
    {
        var c=Archer(true);c=c with{Equipment=[..c.Equipment.Select(i=>i.Id=="arrows"?i with{Carried=false}:i)]};
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Roll(Workspace(c),Attack("longbow")));
    }

    [TestMethod]public void RecoveringReturnsHalfTheFiredAmmunitionRoundedDownAndStartsTheCountOver()
    {
        var c=Archer(true,10) with{Sheet=new(){CountAmmunition=true,AmmunitionFired=new(){["Arrow"]=7}}};
        var recovered=AmmunitionRules.Recover(c,"Arrow");
        Assert.AreEqual(13,Arrows(recovered));Assert.AreEqual(0,recovered.Sheet.AmmunitionFired.Count);
        Assert.ThrowsExactly<ArgumentException>(()=>AmmunitionRules.Recover(recovered,"Arrow"));
    }

    [TestMethod]public void TurningCountingOffForgetsFiredAmmunition()
    {
        var c=Archer(true) with{Sheet=new(){CountAmmunition=true,AmmunitionFired=new(){["Arrow"]=3}}};
        var off=AmmunitionRules.SetCounting(c,false);
        Assert.IsFalse(off.Sheet.CountAmmunition);Assert.AreEqual(0,off.Sheet.AmmunitionFired.Count);
        Assert.IsTrue(AmmunitionRules.SetCounting(off,true).Sheet.CountAmmunition);
    }

    [TestMethod]public void AnItemNamedInThePluralIsTheSameAmmunition()
    {
        Assert.IsTrue(AmmunitionRules.Is(new(){Name="Arrows"},"Arrow"));Assert.IsTrue(AmmunitionRules.Is(new(){Name="Bolt"},"Bolt"));
        Assert.IsFalse(AmmunitionRules.Is(new(){Name="Bolts"},"Arrow"));Assert.AreEqual("Sling Bullets",AmmunitionRules.Plural("Sling Bullet"));
    }
}
