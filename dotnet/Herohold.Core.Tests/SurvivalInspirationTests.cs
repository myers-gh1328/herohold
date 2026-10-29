using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class SurvivalInspirationTests
{
    [TestMethod]
    [DataRow(0,0,1,false,1,false)]
    [DataRow(0,0,1,true,2,false)]
    [DataRow(0,9,1,false,1,false)]
    [DataRow(0,0,44,false,3,true)]
    [DataRow(1,0,44,false,0,false)]
    [DataRow(1,0,45,false,3,true)]
    public void DamageAtZeroAndMassiveDamageTrackFailuresAndDeath(int hp,int temporary,int amount,bool critical,int failures,bool dead)
    {
        var c=Fixtures.Ranger() with{CurrentHp=hp,TemporaryHp=temporary};var r=PlayRules.ChangeHealth(new(){Character=c,Revision=1},new(){SourceCharacterId=c.Id,SourceRevision=1,OperationId="damage",Kind=HealthChangeKind.Damage,Amount=amount,CriticalDamage=critical});Assert.AreEqual(failures,r.Sheet.DeathSaves.Failures);Assert.AreEqual(dead,r.Sheet.DeathSaves.Dead);Assert.AreEqual(0,r.CurrentHp);
    }
    [TestMethod]public void HealingResetsDeathSavesAndUndoRestoresThemWithoutChangingProfile()
    {
        var c=Fixtures.Ranger() with{CurrentHp=0,Sheet=new(){DeathSaves=new(){Successes=1,Failures=2},Profile=new(){Appearance="Unchanged"}}};var r=PlayRules.ChangeHealth(new(){Character=c,Revision=1},new(){SourceCharacterId=c.Id,SourceRevision=1,OperationId="heal",Kind=HealthChangeKind.Healing,Amount=1});Assert.AreEqual(new DeathSaveState(),r.Sheet.DeathSaves);var undo=PlayRules.Undo(new(){Character=r,Revision=2},new(){SourceCharacterId=c.Id,SourceRevision=2,OperationId="undo"});Assert.AreEqual(2,undo.Sheet.DeathSaves.Failures);Assert.AreEqual("Unchanged",undo.Sheet.Profile.Appearance);
        var dead=c with{Sheet=c.Sheet with{DeathSaves=new(){Dead=true,Failures=3}}};Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.ChangeHealth(new(){Character=dead,Revision=1},new(){SourceRevision=1,OperationId="heal-dead",Kind=HealthChangeKind.Healing,Amount=1}));
    }
    [TestMethod]public void HeroicInspirationRerollsOneDieMustKeepWorseResultAndNeverSpendsLuck()
    {
        var c=Fixtures.Ranger() with{Sheet=new(){HeroicInspiration=true}};var rolled=PlayRules.Roll(new(){Character=c,Revision=1},new(){OperationId="roll",SourceRevision=1,Kind=RollKind.Save,Ability=Ability.Wisdom,Mode=DiceMode.Physical,PhysicalD20=[18]});
        var request=new HeroicInspirationRequest{OperationId="heroic",SourceCharacterId=c.Id,SourceRevision=2,RollId="roll",DieIndex=0,PhysicalD20=2};var used=PlayRules.UseHeroicInspiration(new(){Character=rolled,Revision=2},request);Assert.AreEqual(2,used.Play.LastRoll!.ChosenD20);Assert.IsFalse(SheetRules.HasHeroicInspiration(used));Assert.AreEqual(3,used.Resources.Single(r=>r.Id=="luck").Remaining);
        var retried=PlayRules.UseHeroicInspiration(new(){Character=used,Revision=3},request);Assert.AreEqual(used.Play.LastRoll.Total,retried.Play.LastRoll!.Total);
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.UseHeroicInspiration(new(){Character=used,Revision=3},request with{OperationId="again",SourceRevision=3}));
        var undo=PlayRules.Undo(new(){Character=used,Revision=3},new(){OperationId="undo-heroic",SourceRevision=3});Assert.IsTrue(SheetRules.HasHeroicInspiration(undo));
    }
    [TestMethod]public void HeroicInspirationCanRerollOneDamageDieButNotAnOldDamageWindow()
    {
        var c=Fixtures.Ranger() with{Sheet=new(){HeroicInspiration=true},Play=new(){CompletedOperationIds=["damage"],LastRoll=new(){Id="attack",Name="Bow",Kind=RollKind.Attack,Mode=DiceMode.Physical,D20=[15],ChosenD20=15,Total=15,Resolved=true,Hit=true,InspirationWindowId="damage",Damage=[new(){Name="Weapon",DamageType="Piercing",DieSides=8,Dice=[8],Modifier=4,Total=12}]}}};
        var request=new HeroicInspirationRequest{OperationId="heroic-damage",SourceCharacterId=c.Id,SourceRevision=1,Kind=HeroicDieKind.Damage,RollId="attack",DamageTermIndex=0,DieIndex=0,PhysicalDie=1};
        var used=PlayRules.UseHeroicInspiration(new(){Character=c,Revision=1},request);Assert.AreEqual(1,used.Play.LastRoll!.Damage[0].Dice[0]);Assert.AreEqual(5,used.Play.LastRoll.Damage[0].Total);Assert.IsFalse(SheetRules.HasHeroicInspiration(used));Assert.AreEqual(8,c.Play.LastRoll!.Damage[0].Dice[0]);
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.UseHeroicInspiration(new(){Character=c with{Play=c.Play with{CompletedOperationIds=["damage","another-action"]}},Revision=1},request));
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.UseHeroicInspiration(new(){Character=c,Revision=1},request with{PhysicalDie=9}));
    }
    [TestMethod]public void HeroicInspirationCanRerollOneSpellEffectDieAndRecalculateItsTotal()
    {
        var c=Fixtures.Ranger() with{Sheet=new(){HeroicInspiration=true},Play=new(){CompletedOperationIds=["effect"],LastCast=new(){Id="cast",Mode=DiceMode.Physical,InspirationWindowId="effect",EffectDice=[8,7],EffectTotal=18,Preview=new(){SpellId="cure",Name="Cure Wounds",Dice=new(2,8),Modifier=3}}}};
        var r=PlayRules.UseHeroicInspiration(new(){Character=c,Revision=1},new(){OperationId="heroic-effect",SourceCharacterId=c.Id,SourceRevision=1,Kind=HeroicDieKind.SpellEffect,CastId="cast",DieIndex=1,PhysicalDie=1});Assert.AreEqual(12,r.Play.LastCast!.EffectTotal);Assert.AreEqual(8,r.Play.LastCast.EffectDice[0]);Assert.AreEqual(1,r.Play.LastCast.EffectDice[1]);Assert.IsFalse(SheetRules.HasHeroicInspiration(r));
    }
    [TestMethod]public void HeroicInspirationCanRerollOneBonusDieWithoutChangingTheD20()
    {
        var c=Fixtures.Ranger() with{Sheet=new(){HeroicInspiration=true}};var rolled=PlayRules.Roll(new(){Character=c,Revision=1},new(){OperationId="bonus-roll",SourceRevision=1,Kind=RollKind.Save,Ability=Ability.Wisdom,Mode=DiceMode.Physical,PhysicalD20=[12],BonusDice=new(1,4),PhysicalBonusDice=[4]});
        var used=PlayRules.UseHeroicInspiration(new(){Character=rolled,Revision=2},new(){OperationId="heroic-bonus",SourceRevision=2,Kind=HeroicDieKind.BonusDie,RollId="bonus-roll",DieIndex=0,PhysicalDie=1});Assert.AreEqual(12,used.Play.LastRoll!.ChosenD20);Assert.AreEqual(1,used.Play.LastRoll.BonusDice.Single());Assert.AreEqual(rolled.Play.LastRoll!.Total-3,used.Play.LastRoll.Total);
    }
}
