using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class AdventureFactsTests
{
    [TestMethod]
    public void InventorySummaryIncludesQuantityCoinsSizeAndUnknownWeightsWithoutPretendingCompleteness()
    {
        var c=new CharacterState{Id="traveler",Name="Traveler"} with{AbilityScores=Enum.GetValues<Ability>().ToDictionary(a=>a,_=>10),Sheet=new(){Coins=new(){Gp=50},Profile=new(){Size="Small"}},Equipment=[new(){Id="rope",Name="Rope",Quantity=2,UnitWeightLb=10},new(){Id="mystery",Name="Unweighed item"}]};
        var total=AdventureRules.Carrying(c);
        Assert.AreEqual(21m,total.KnownWeightLb);Assert.AreEqual(150m,total.CapacityLb);Assert.AreEqual(1,total.UnweighedItems);Assert.IsFalse(total.Complete);
        Assert.AreEqual(75m,AdventureRules.Carrying(c with{Sheet=c.Sheet with{Profile=new(){Size="Tiny"}}}).CapacityLb);
        Assert.AreEqual(300m,AdventureRules.Carrying(c with{Sheet=c.Sheet with{Profile=new(){Size="Large"}}}).CapacityLb);
    }

    [TestMethod]
    public void ChargedItemSpendsOnlyItsChargesAndRejectsOverspendingAtomically()
    {
        var c=new CharacterState{Id="traveler",Name="Traveler",Equipment=[new(){Id="wand",Name="Wand",Charges=new(){Maximum=7,Remaining=2}}]};
        var used=AdventureRules.AdjustCharges(c,"wand",-2);
        Assert.AreEqual(0,used.Equipment[0].Charges!.Remaining);Assert.AreEqual(2,c.Equipment[0].Charges!.Remaining);
        Assert.ThrowsExactly<ArgumentException>(()=>AdventureRules.AdjustCharges(used,"wand",-1));
        Assert.AreEqual(1,used.Equipment[0].Quantity);
    }

    [TestMethod]
    public void CompanionAndMovementEditsAreDetachedAndCompanionDamageNeverChangesPlayerHp()
    {
        var c=new CharacterState{Id="traveler",Name="Traveler",CurrentHp=12,MaximumHp=20};
        var facts=new AdventureFacts{Movement=[new(){Kind="Fly",Speed=40,Source="Wings"}],Companions=[new(){Id="owl",Name="Owl",Kind="Familiar",MaximumHp=5,CurrentHp=5,Actions=[new(){Name="Help",Rules="Within reach"}]}]};
        var updated=AdventureRules.SetFacts(c,facts);facts.Companions.Clear();
        Assert.AreEqual(1,updated.Sheet.Adventure.Companions.Count);Assert.AreEqual(0,c.Sheet.Adventure.Companions.Count);
        var damaged=AdventureRules.AdjustCompanionHp(updated,"owl",-3);
        Assert.AreEqual(2,damaged.Sheet.Adventure.Companions[0].CurrentHp);Assert.AreEqual(12,damaged.CurrentHp);
        Assert.AreEqual(30,AdventureRules.Movement(damaged with{Exhaustion=2}).Single(m=>m.Kind=="Fly").Speed);
        Assert.ThrowsExactly<ArgumentException>(()=>AdventureRules.SetFacts(c,new(){Movement=[new(){Kind="Teleport",Speed=-3}]}));
    }

    [TestMethod]
    public void UsingAChargedItemSpendsAChargeAndAppliesItsEffectTogether()
    {
        var c=new CharacterState{Id="traveler",Name="Traveler",Equipment=[new(){Id="wand",Name="Wand",Charges=new(){Maximum=7,Remaining=1},UseEffects=[new(){Name="Light"}]}]};
        var used=SheetRules.UseEquipment(c,"wand");
        Assert.AreEqual(0,used.Equipment[0].Charges!.Remaining);Assert.AreEqual(1,used.Effects.Count);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(used,"wand"));Assert.AreEqual(1,used.Effects.Count);
    }

    [TestMethod]
    public void ItemUseInCombatRequiresConfirmedRecordedCostAndSpendsItAtomically()
    {
        var c=new CharacterState{Id="traveler",Name="Traveler",CurrentHp=10,MaximumHp=10,Combat=new(){Enabled=true,EncounterActive=true,EncounterId="fight",TurnId="turn",Phase=CombatPhase.OwnTurn,ActionAvailable=true,BonusActionAvailable=true},Equipment=[new(){Id="potion",Name="Potion",Consumable=true,UseAction=ItemUseAction.BonusAction}]};
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(c,"potion"));
        var used=SheetRules.UseEquipment(c,"potion",true);Assert.AreEqual(0,used.Equipment[0].Quantity);Assert.IsFalse(used.Combat.BonusActionAvailable);Assert.IsTrue(used.Combat.ActionAvailable);
        var repeated=c with{Combat=c.Combat with{BonusActionAvailable=false}};Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(repeated,"potion",true));Assert.AreEqual(1,repeated.Equipment[0].Quantity);
    }
}
