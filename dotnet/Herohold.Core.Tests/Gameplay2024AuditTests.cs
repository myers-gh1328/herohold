using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class Gameplay2024AuditTests
{
    private static WorkspaceSnapshot Workspace(CharacterState? c=null,long revision=1)=>new(){Revision=revision,Character=c??Fixtures.Ranger()};
    [TestMethod]public void ZeroHitPointsCannotStartANewConcentrationEffect()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.AddEffect(Fixtures.Ranger() with{CurrentHp=0},new(){Id="new-mark",Name="Hunter’s Mark",Concentration=true}));
    }
    [TestMethod]public void MovingExistingHuntersMarkNeedsConfirmedDefeatAndSpendsNoAdditionalCast()
    {
        var c=Fixtures.Ranger() with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=new(){HunterTarget="First quarry"}};
        var request=new HunterMarkRetargetRequest{OperationId="transfer-mark",SourceRevision=1,Target="Next quarry",PreviousTargetAtZeroHp=true};
        var moved=PlayRules.RetargetHuntersMark(Workspace(c),request);Assert.AreEqual("Next quarry",moved.Play.HunterTarget);Assert.AreEqual(c.Resources.Single().Remaining,moved.Resources.Single().Remaining);Assert.AreEqual("mark",moved.Effects.Single().Id);
        Assert.AreEqual("Next quarry",PlayRules.RetargetHuntersMark(Workspace(moved,2),request).Play.HunterTarget);
        var undo=PlayRules.Undo(Workspace(moved,2),new(){OperationId="undo-mark-transfer",SourceRevision=2});Assert.AreEqual("First quarry",undo.Play.HunterTarget);
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.RetargetHuntersMark(Workspace(c),request with{PreviousTargetAtZeroHp=false}));
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.RetargetHuntersMark(Workspace(c with{Effects=[]}),request));
    }
    [TestMethod]public void RitualAndLongCastingReplaceEarlierConcentrationOnlyWhenTheCastIsConfirmed()
    {
        foreach(var ritual in new[]{false,true})
        {
            var spell=new SpellDefinition{Id="long-cast",Name="Fictional long cast",Level=1,Ritual=true,CastingTime=ritual?"1 action":"1 minute"};
            var c=Fixtures.Ranger() with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=new(){HunterTarget="Quarry",GrantedSpellIds=[spell.Id],Spells=[spell]},Resources=[new(){Id="slot1",Name="Slots",Maximum=2,Remaining=2,SlotLevel=1}]};
            var preview=SpellRules.Preview(c,spell.Id,1);StringAssert.Contains(preview.Note,"Concentration");Assert.IsTrue(c.Effects.Any(effect=>effect.Concentration));
            var cast=PlayRules.Cast(Workspace(c),new(){OperationId="long-cast-action",SourceRevision=1,SpellId=spell.Id,SlotLevel=1,PaymentResourceId=ritual?"none":"slot1",Ritual=ritual});
            Assert.IsFalse(cast.Effects.Any(effect=>effect.Concentration));Assert.AreEqual("",cast.Play.HunterTarget);
            var blocked=c with{Effects=[new(){Id="stun",Name="Stunned"}]};
            Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Cast(Workspace(blocked),new(){OperationId="incapacitated-long-cast",SourceRevision=1,SpellId=spell.Id,SlotLevel=1,PaymentResourceId=ritual?"none":"slot1",Ritual=ritual}));
        }
    }
    [TestMethod]public void ShortRestEachSpentHitDieHealsAtLeastOneEvenWithNegativeConstitution()
    {
        var c=Fixtures.Ranger() with{CurrentHp=10,AbilityScores=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>a==Ability.Constitution?1:10),Resources=[new(){Id="hitdice",Name="Hit Dice",Maximum=5,Remaining=5}]};
        var result=RestRules.Preview(Workspace(c),new(){OperationId="minimum-hit-die",SourceRevision=1,Kind=RestKind.Short,Mode=DiceMode.Physical,HitDiceToSpend=2,PhysicalHitDice=[1,2]});
        Assert.AreEqual(12,result.CurrentHp);Assert.AreEqual(3,result.Resources.Single().Remaining);
    }
    [TestMethod]public void NeitherRestCanStartAtZeroHitPoints()
    {
        foreach(var kind in new[]{RestKind.Short,RestKind.Long})Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(Workspace(Fixtures.Ranger() with{CurrentHp=0}),new(){OperationId="zero-hp-rest",SourceRevision=1,Kind=kind}));
    }
    [TestMethod]public void StalkersFlurryIncreasesDreadfulStrikeDieAtLevelElevenAndCriticalDoublesDiceOnly()
    {
        foreach(var level in new[]{10,11})
        {
            var c=Fixtures.Ranger() with{Level=level,Resources=[new(){Id="dread",Name="Dreadful Strike",Maximum=3,Remaining=3}]};
            var rolled=PlayRules.Roll(Workspace(c),new(){OperationId="critical-attack",SourceRevision=1,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,Mode=DiceMode.Physical,PhysicalD20=[20]});
            var hit=PlayRules.ResolveAttack(Workspace(rolled,2),new(){OperationId="critical-hit",SourceRevision=2,RollId="critical-attack",Hit=true,DreadfulStrike=true,BaseDamage=new(1,8),DamageModifier=4,PhysicalDamageDice=new[]{1,1,8,8,8,8}.Select(die=>level==10&&die==8?6:die).ToList()});
            Assert.AreEqual(level==10?24:32,hit.Play.LastRoll!.Damage.Single(term=>term.Name=="Dreadful Strike").Total);
            Assert.AreEqual(6,hit.Play.LastRoll.Damage.Single(term=>term.Name=="Weapon").Total);Assert.AreEqual(2,hit.Resources.Single().Remaining);
        }
    }
    [TestMethod]public void NaturalOneAndTwentyCannotBeConfirmedAsContradictoryAttackOutcomes()
    {
        foreach(var value in new[]{1,20})
        {
            var rolled=PlayRules.Roll(Workspace(),new(){OperationId="natural-attack",SourceRevision=1,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,Mode=DiceMode.Physical,PhysicalD20=[value]});
            Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.ResolveAttack(Workspace(rolled,2),new(){OperationId="contradictory-hit",SourceRevision=2,RollId="natural-attack",Hit=value==1,PhysicalDamageDice=[1]}));
        }
    }
    [TestMethod]public void ReachingZeroHitPointsEndsConcentrationAndMarkButKeepsUnrelatedEffectsAndUndo()
    {
        var c=Fixtures.Ranger() with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true},new(){Id="poison",Name="Poisoned"}],Play=new(){HunterTarget="Fictional quarry"}};
        var zero=PlayRules.AdjustHp(Workspace(c),new(){OperationId="zero-hp",SourceRevision=1,Delta=-c.CurrentHp});
        Assert.IsFalse(zero.Effects.Any(effect=>effect.Concentration));Assert.AreEqual("",zero.Play.HunterTarget);Assert.IsTrue(zero.Effects.Any(effect=>effect.Id=="poison"));
        var undo=PlayRules.Undo(Workspace(zero,2),new(){OperationId="undo-zero-hp",SourceRevision=2});Assert.AreEqual(c.CurrentHp,undo.CurrentHp);Assert.AreEqual("Fictional quarry",undo.Play.HunterTarget);Assert.IsTrue(undo.Effects.Any(effect=>effect.Concentration));
    }
}
