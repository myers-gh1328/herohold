using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class SheetStateTests
{
    [TestMethod]
    public void PurseUsesImportedValuesWithoutMutatingAndCompoundSpendIsAtomic()
    {
        var c=Fixtures.Ranger() with{Build=new(){Background="Sailor",PdfPassthroughFields=new(){["GP"]=new(){Text="12"},["SP"]=new(){Text="4"},["Backstory"]=new(){Text="Across the sea"}}},Derived=new(){Languages=["Common","Elvish"]}};
        Assert.AreEqual(12,SheetRules.Purse(c).Gp);Assert.IsNull(c.Sheet.Coins);Assert.AreEqual("Sailor",SheetRules.Profile(c).Background);Assert.AreEqual("Across the sea",SheetRules.Profile(c).Backstory);
        var paid=SheetRules.ChangeCoins(c,CoinChangeKind.Spend,new(){Gp=2,Sp=3});Assert.AreEqual(10,SheetRules.Purse(paid).Gp);Assert.AreEqual(1,SheetRules.Purse(paid).Sp);Assert.AreEqual(12,SheetRules.Purse(c).Gp);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.ChangeCoins(paid,CoinChangeKind.Spend,new(){Gp=1,Sp=2}));Assert.AreEqual(10,SheetRules.Purse(paid).Gp);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.ChangeCoins(paid,CoinChangeKind.Receive,new(){Cp=-1}));
        var capped=SheetRules.ChangeCoins(c,CoinChangeKind.Set,new(){Pp=1_000_000_000});Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.ChangeCoins(capped,CoinChangeKind.Receive,new(){Pp=1}));
    }
    [TestMethod]
    public void ProfileOverridesAreDetachedAndDoNotOverwriteTheBuildRules()
    {
        var c=Fixtures.Ranger() with{Build=new(){Background="Sailor"},Derived=new(){Size="Medium",Languages=["Common"]}};
        var p=new CharacterProfile{Appearance="Silver hair",Background="Recorded background",Languages=["Common","Dwarvish"],Ideals="Mercy"};
        var result=SheetRules.UpdateProfile(c,p,12345);p.Languages!.Add("Infernal");Assert.AreEqual(2,SheetRules.Profile(result).Languages!.Count);Assert.AreEqual(12345,SheetRules.ExperiencePoints(result));Assert.AreEqual("Sailor",result.Build!.Background);Assert.AreEqual("Medium",SheetRules.Profile(result).Size);Assert.IsNull(c.Sheet.ExperiencePoints);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UpdateProfile(c,p,-1));
    }
    [TestMethod]
    [DataRow(1,0,2,0,false,false)]
    [DataRow(9,0,1,0,false,false)]
    [DataRow(10,1,0,0,false,false)]
    [DataRow(19,1,0,0,false,false)]
    [DataRow(20,0,0,1,false,false)]
    public void DeathSavesUseUnmodifiedD20AndNaturalBoundaries(int die,int successes,int failures,int hp,bool stable,bool dead)
    {
        var c=Fixtures.Ranger() with{CurrentHp=0};var r=SheetRules.RecordDeathSave(c,die);Assert.AreEqual(successes,r.Sheet.DeathSaves.Successes);Assert.AreEqual(failures,r.Sheet.DeathSaves.Failures);Assert.AreEqual(hp,r.CurrentHp);Assert.AreEqual(stable,r.Sheet.DeathSaves.Stable);Assert.AreEqual(dead,r.Sheet.DeathSaves.Dead);Assert.AreEqual(0,c.Sheet.DeathSaves.Successes);
    }
    [TestMethod]
    public void ThreeSuccessesResetBothCountsAndStabilizeWhileThreeFailuresKill()
    {
        var c=Fixtures.Ranger() with{CurrentHp=0,Sheet=new(){DeathSaves=new(){Successes=2,Failures=1}}};var stable=SheetRules.RecordDeathSave(c,10);Assert.IsTrue(stable.Sheet.DeathSaves.Stable);Assert.AreEqual(0,stable.Sheet.DeathSaves.Successes);Assert.AreEqual(0,stable.Sheet.DeathSaves.Failures);Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.RecordDeathSave(stable,10));
        var dead=SheetRules.RecordDeathSave(c with{Sheet=new(){DeathSaves=new(){Failures=2}}},2);Assert.IsTrue(dead.Sheet.DeathSaves.Dead);Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.RecordDeathSave(dead,20));
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.RecordDeathSave(Fixtures.Ranger(),10));Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.RecordDeathSave(c,0));
    }
    [TestMethod]
    public void EquipmentFactsRequireActiveSourceAndUseEffectsRemainIndependent()
    {
        var item=new EquipmentItem{Id="periapt",Name="Periapt",Quantity=1,RequiresAttunement=true,Defenses=[new(){Kind="immunity",Type="Poison"}],PassiveEffects=[new(){Id="ward",Name="Warded"}]};
        var c=Fixtures.Ranger() with{Equipment=[item],Defenses=[new(){Kind="immunity",Type="Poison",Source="Species"}]};Assert.AreEqual(1,SheetRules.EffectiveDefenses(c).Count);
        var active=c with{Equipment=[item with{Equipped=true,Attuned=true}]};Assert.AreEqual(2,SheetRules.EffectiveDefenses(active).Count);Assert.AreEqual(1,SheetRules.EffectiveEffects(active).Count);Assert.AreEqual(0,c.Effects.Count);
        var potion=new EquipmentItem{Id="invisibility",Name="Potion",Quantity=1,Consumable=true,UseEffects=[new(){Id="invisible",Name="Invisible",Note="1 hour"}]};
        var used=SheetRules.UseEquipment(c with{Equipment=[potion],Effects=[new(){Id="manual-invisible",Name="Invisible",Source="Other source"}]},potion.Id);Assert.AreEqual(0,used.Equipment.Single().Quantity);Assert.AreEqual(2,used.Effects.Count);Assert.IsTrue(used.Effects.Any(e=>e.Id=="manual-invisible"));Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(used,potion.Id));
    }
    [TestMethod]
    [DataRow("coins")][DataRow("inspiration")][DataRow("survival")]
    public void SheetMutationsNeverAliasOriginalProfileLists(string action)
    {
        var c=Fixtures.Ranger() with{CurrentHp=0,Sheet=new(){Profile=new(){Languages=["Common"]}}};
        var result=action=="coins"?SheetRules.ChangeCoins(c,CoinChangeKind.Receive,new(){Gp=1}):action=="inspiration"?SheetRules.SetHeroicInspiration(c,true):SheetRules.SetDeathSaves(c,new(){Successes=1});
        c.Sheet.Profile.Languages!.Add("Elvish");Assert.AreEqual(1,result.Sheet.Profile.Languages!.Count);
    }
    [TestMethod]public void InvalidImportedNumbersStayUnchangedUntilExplicitRepair()
    {
        var c=Fixtures.Ranger() with{Build=new(){PdfPassthroughFields=new(){["GP"]=new(){Text="12.5"},["XP"]=new(){Text="unreadable"}}}};Assert.AreEqual(2,SheetRules.ImportWarnings(c).Count);var changed=SheetRules.UpdateProfile(c,new(){Alignment="Neutral"},null);Assert.IsNull(changed.Sheet.ExperiencePoints);Assert.IsNull(changed.Sheet.Coins);Assert.AreEqual("12.5",changed.Build!.PdfPassthroughFields!["GP"].Text);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.ChangeCoins(c,CoinChangeKind.Receive,new(){Gp=1}));var repaired=SheetRules.UpdateProfile(SheetRules.ChangeCoins(c,CoinChangeKind.Set,new(){Gp=12,Sp=5}),new(),500);Assert.AreEqual(0,SheetRules.ImportWarnings(repaired).Count);
    }
}
