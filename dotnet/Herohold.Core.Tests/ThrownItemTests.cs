using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class ThrownItemTests
{
    private static readonly EquipmentItem Fire=new(){Id="fire",Name="Alchemist's Fire",Quantity=2,Consumable=true,ThrownRangeFt=20,ThrownDamage=new(1,4),ThrownDamageType="Fire"};
    private static CharacterState Thrower(int flasks=2)=>Fixtures.Ranger() with{Equipment=[Fire with{Quantity=flasks}]};
    private static CharacterState InCombat(CharacterState c)=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="e",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="t1"});

    [TestMethod]public void TheSaveDcIsEightPlusDexterityPlusProficiency()
    {
        var dc=ThrownItemRules.SaveDc(Fixtures.Ranger());
        Assert.AreEqual(15,dc.Total,"8 + Dexterity 18 (+4) + level 5 proficiency (+3).");
        Assert.AreEqual("Base",dc.Terms[0].Label);
    }

    [TestMethod]public void ThrowingOutsideCombatSpendsOneAndNoneLeftIsRefused()
    {
        var c=ThrownItemRules.Throw(Thrower(1),"fire");
        Assert.AreEqual(0,c.Equipment.Single().Quantity);
        Assert.AreEqual("None left in your pack.",ThrownItemRules.Reason(c,c.Equipment.Single()));
        Assert.ThrowsExactly<ArgumentException>(()=>ThrownItemRules.Throw(c,"fire"));
        Assert.ThrowsExactly<ArgumentException>(()=>ThrownItemRules.Throw(Fixtures.Ranger(),"potion"),"A potion is not thrown in place of an attack.");
    }

    [TestMethod]public void InCombatAThrowReplacesOneAttackOfTheAttackAction()
    {
        var c=InCombat(Thrower());
        Assert.IsTrue(c.Combat.ActionAvailable);Assert.AreEqual(0,c.Combat.AttacksRemaining);
        var thrown=ThrownItemRules.Throw(c,"fire");
        Assert.IsFalse(thrown.Combat.ActionAvailable,"The first throw takes the Attack action.");
        Assert.AreEqual(CombatRules.Spend(c,"attack").Combat.AttacksRemaining-1,thrown.Combat.AttacksRemaining);Assert.AreEqual(1,thrown.Equipment.Single().Quantity);
    }

    [TestMethod]public void ThrowingWaitsForYourOwnTurn()
    {
        var c=InCombat(Thrower());
        var waiting=c with{Combat=c.Combat with{Phase=CombatPhase.BetweenTurns}};
        Assert.IsNotNull(ThrownItemRules.Reason(waiting,waiting.Equipment.Single()));
        Assert.ThrowsExactly<ArgumentException>(()=>ThrownItemRules.Throw(waiting,"fire"));
    }

    [TestMethod]public void ThrownDamageTextReadsAndWrites()
    {
        Assert.AreEqual("1d4 Fire",ItemLinking.ThrownDamageText(Fire));
        Assert.AreEqual((new DiceFormula(2,6),"Acid"),ItemLinking.ParseThrownDamage("2d6 acid"));
        Assert.IsNull(ItemLinking.ParseThrownDamage("2d6"));Assert.IsNull(ItemLinking.ParseThrownDamage("lots of fire"));
    }

    [TestMethod]public void ThrownFactsOutsideTheirBoundsAreInvalid()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[Fire with{ThrownRangeFt=null}]}),"Damage needs a range.");
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[Fire with{ThrownDamageType=""}]}),"Damage needs a type.");
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[Fire with{ThrownRangeFt=2}]}));
        WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[new(){Id="net",Name="Net",ThrownRangeFt=15}]});
    }
}
