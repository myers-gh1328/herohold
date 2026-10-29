using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class PassiveIncapacitationTests
{
    private static CharacterState Character()=>new()
    {
        Id="passive-condition",Name="Condition example",ClassName="Ranger",Level=3,MaximumHp=20,CurrentHp=20,
        Effects=[new(){Id="mark",Name="Hunter's Mark",Source="spell:hunters-mark",Concentration=true}],
        Play=new(){HunterTarget="Marked creature"},
        Equipment=[new(){Id="curse",Name="Cursed clasp",PassiveEffects=[new(){Name="Paralyzed"}]}]
    };

    [TestMethod]
    public void ActivatingAnIncapacitatingItemEndsOnlyConcentrationAndDoesNotRestoreItOnRemoval()
    {
        var before=Character();
        var after=WorkspaceRules.SetEquipment(before,"curse",true,false);
        Assert.IsFalse(after.Effects.Any(e=>e.Concentration));
        Assert.AreEqual("",after.Play.HunterTarget);
        Assert.IsTrue(SheetRules.EffectiveEffects(after).Any(e=>e.Name=="Paralyzed"));
        Assert.IsTrue(before.Effects.Any(e=>e.Concentration),"The source snapshot stays detached.");
        var removed=WorkspaceRules.SetEquipment(after,"curse",false,false);
        Assert.IsFalse(removed.Effects.Any(e=>e.Concentration));
    }

    [TestMethod]
    public void APassiveIncapacitatingConditionBlocksStartingNewConcentration()
    {
        var before=Character() with{Effects=[],Equipment=[Character().Equipment[0] with{Equipped=true}]};
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.AddEffect(before,new(){Id="new-mark",Name="Hunter's Mark",Source="spell:hunters-mark",Concentration=true}));
    }
}
