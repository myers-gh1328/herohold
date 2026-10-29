using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class MulticlassPlayTests
{
    internal static CharacterState Character()
    {
        var c=Fixtures.Ranger();
        return c with { CurrentHp=1,MaximumHp=40,Play=c.Play with {
            Spells=[new(){Id="wizard-a",Name="Shield",Level=1},new(){Id="wizard-b",Name="Detect Magic",Level=1},new(){Id="wizard-high",Name="Fireball",Level=3},new(){Id="ranger-a",Name="Cure Wounds",Level=1},new(){Id="ranger-b",Name="Goodberry",Level=1}],
            SpellSources=[new(){Id="wizard",Name="Wizard",Ability=Ability.Intelligence,PreparedSpellIds=["wizard-a"],SpellbookIds=["wizard-a","wizard-b","wizard-high"],Preparation=new(){MaximumSpellLevel=1,CanChangeOnLongRest=true,PreparedLimit=2,FromSpellbook=true,EligibleSpellIds=["wizard-a","wizard-b","wizard-high"]}},new(){Id="ranger",Name="Ranger",Ability=Ability.Wisdom,PreparedSpellIds=["ranger-a"],Preparation=new(){MaximumSpellLevel=1,CanChangeOnLongRest=true,PreparedLimit=2,ReplacementLimit=1,EligibleSpellIds=["ranger-a","ranger-b"]}}],
            HitDiePools=[new(){Id="hd6",Sides=6,Maximum=1,Remaining=1,ClassTrackIds=["wizard"]},new(){Id="hd10",Sides=10,Maximum=2,Remaining=1,ClassTrackIds=["ranger"]}]
        },Resources=[new(){Id="slot3",Name="Shared third",Maximum=2,Remaining=2,SlotLevel=3},new(){Id="pact-slot-1",Name="Pact",Maximum=1,Remaining=0,SlotLevel=1,Recovery=RecoveryRest.Short}]};
    }
    [TestMethod] public void SourceRestIsIsolatedAndSharedSlotsDoNotExpandSpellAccess()
    {
        var c=Character();var w=new WorkspaceSnapshot{Character=c};
        var request=new RestRequest{Kind=RestKind.Long,SourceChanges=[new(){SpellSourceId="wizard",ChangeSpells=true,NewPreparedSpellIds=["wizard-b"]}]};
        var preview=RestRules.Preview(w,request);
        CollectionAssert.AreEqual(new[]{"wizard-b"},preview.SpellSources.Single(x=>x.Id=="wizard").PreparedSpellIds);
        CollectionAssert.AreEqual(new[]{"ranger-a"},preview.SpellSources.Single(x=>x.Id=="ranger").PreparedSpellIds);
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(w,request with{SourceChanges=[new(){SpellSourceId="wizard",ChangeSpells=true,NewPreparedSpellIds=["wizard-high"]}]}));
        Assert.AreEqual("wizard-a",c.Play.SpellSources[0].PreparedSpellIds[0]);
    }
    [TestMethod] public void MixedHitDiceSpendChosenPoolsAndUndoRestoresAllPools()
    {
        var c=Character();var w=new WorkspaceSnapshot{Character=c};var r=new RestRequest{Kind=RestKind.Short,Mode=DiceMode.Physical,HitDieSpends=[new(){PoolId="hd6",Count=1,PhysicalDice=[6]},new(){PoolId="hd10",Count=1,PhysicalDice=[10]}]};
        var after=PlayRules.Rest(w,r);Assert.AreEqual(0,after.Play.HitDiePools.Sum(p=>p.Remaining));Assert.IsTrue(after.CurrentHp>c.CurrentHp);Assert.AreEqual(1,after.Resources.Single(p=>p.Id=="pact-slot-1").Remaining);
        var undone=PlayRules.Undo(w with{Character=after},new UndoRequest());Assert.AreEqual(2,undone.Play.HitDiePools.Sum(p=>p.Remaining));Assert.AreEqual(1,undone.CurrentHp);
    }
    [TestMethod] public void InvalidLaterPoolOrSourceFailsBeforeAnyRandomRoll()
    {
        var w=new WorkspaceSnapshot{Character=Character()};var rolls=0;
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(w,new(){Kind=RestKind.Short,HitDieSpends=[new(){PoolId="hd6",Count=1},new(){PoolId="hd10",Count=2}]},_=>{rolls++;return 1;}));Assert.AreEqual(0,rolls);
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(w,new(){Kind=RestKind.Short,HitDieSpends=[new(){PoolId="hd6",Count=1}],SourceChanges=[new(){SpellSourceId="wizard",ChangeSpells=true,NewPreparedSpellIds=["wizard-b"]}]},_=>{rolls++;return 1;}));Assert.AreEqual(0,rolls);
    }
    [TestMethod] public void ExplicitAndLegacyAmbiguityRejectsWithoutMutation()
    {
        var w=new WorkspaceSnapshot{Character=Character()};
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(w,new(){Kind=RestKind.Long,ChangeSpells=true,NewPreparedSpellIds=["wizard-b"]}));
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(w,new(){Kind=RestKind.Short,HitDiceToSpend=1,HitDieSpends=[new(){PoolId="hd6",Count=1}]}));
    }
    [TestMethod] public void CastingUsesOwnershipAbilityAndChosenPactPaymentWithoutGrantingUnpreparedAccess()
    {
        var c=Character();c=c with{Resources=c.Resources.Select(p=>p with{Remaining=p.Maximum}).ToList(),Play=c.Play with{Spells=c.Play.Spells.Select(s=>s with{Modeled=true,EffectKind=SpellEffectKind.Healing,Dice=new(1,6),AddCastingModifier=true}).ToList()}};
        var preview=SpellRules.Preview(c,"wizard-a",1);
        Assert.AreEqual(CharacterCalculations.Ability(c,Ability.Intelligence).Total,preview.Modifier);
        var after=PlayRules.Cast(new(){Character=c},new(){SpellId="wizard-a",SlotLevel=1,PaymentResourceId="pact-slot-1"});
        Assert.AreEqual(0,after.Resources.Single(p=>p.Id=="pact-slot-1").Remaining);Assert.AreEqual(2,after.Resources.Single(p=>p.Id=="slot3").Remaining);
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Cast(new(){Character=c},new(){SpellId="wizard-b",SlotLevel=1,PaymentResourceId="pact-slot-1"}));
    }

    [TestMethod] public void UndoSourceRestPreservesAnUnrelatedLaterSourceEdit()
    {
        var w=new WorkspaceSnapshot{Character=Character()};var rested=PlayRules.Rest(w,new(){Kind=RestKind.Long,SourceChanges=[new(){SpellSourceId="wizard",ChangeSpells=true,NewPreparedSpellIds=["wizard-b"]}]});
        var edited=rested with{Play=rested.Play with{SpellSources=rested.Play.SpellSources.Select(s=>s.Id=="ranger"?s with{PreparedSpellIds=["ranger-b"]}:s).ToList(),PreparedSpellIds=["wizard-b","ranger-b"]}};
        var undone=PlayRules.Undo(w with{Character=edited},new());Assert.AreEqual("wizard-a",undone.Play.SpellSources[0].PreparedSpellIds[0]);Assert.AreEqual("ranger-b",undone.Play.SpellSources[1].PreparedSpellIds[0]);Assert.IsTrue(undone.Play.PreparedSpellIds.Contains("ranger-b"));
    }
    [TestMethod] public void SourcePreviewIsDetachedAndNoChangeRestRetainsEveryMembership()
    {
        var c=Character();var preview=RestRules.Preview(new(){Character=c},new(){Kind=RestKind.Long});preview.SpellSources[0].Preparation.EligibleSpellIds.Clear();preview.HitDiePools[0].ClassTrackIds.Clear();Assert.IsTrue(c.Play.SpellSources[0].Preparation.EligibleSpellIds.Count>0);Assert.AreEqual(1,c.Play.HitDiePools[0].ClassTrackIds.Count);Assert.AreEqual("wizard-a",preview.SpellSources[0].PreparedSpellIds[0]);
    }

    [TestMethod] public void LegacyEligibilityQueryRejectsMultipleEligibleSourcesAndExactGrantProvenanceSurvives()
    {
        var c=Character();Assert.ThrowsExactly<ArgumentException>(()=>RestRules.EligiblePreparations(c));
        var grant=new SpellSourceState{Id="class_feature:Favored Enemy",SourceKind="class_feature",Name="Favored Enemy",Ability=Ability.Wisdom,Preparation=new(){MaximumSpellLevel=1},GrantedSpellIds=["grant-mark"]};
        var allowed=c with{Play=c.Play with{SpellSources=[grant],Spells=[new(){Id="grant-mark",Name="Hunter’s Mark",Level=1}]}};WorkspaceRules.Validate(allowed);Assert.AreEqual(grant.Id,ClassPlayRules.SpellSource(allowed,"grant-mark")!.Id);
    }

}
