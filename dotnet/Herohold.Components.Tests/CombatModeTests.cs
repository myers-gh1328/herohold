using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class CombatModeTests
{
    [TestMethod]
    public void TimerEditorKeepsSourceAndEmitsExplicitBoundaryMetadata()
    {
        using var ctx=new BunitContext();var c=SampleWorkspace.Create().Character with{Effects=[new(){Id="owned",Name="Bless",Source="spell:bless"}]};CombatTransitionRequest? action=null;
        var cut=ctx.Render<CombatTimerEditor>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnTransition,r=>action=r));
        cut.Find("[data-testid='combat-timer-effect']").Change("owned");cut.Find("[data-testid='combat-duration']").Change(nameof(CombatDuration.Rounds));cut.Find("[data-testid='combat-save-timer']").Click();
        Assert.AreEqual(CombatTransitionKind.AddEffect,action!.Kind);Assert.AreEqual("owned",action.Timer!.EffectId);Assert.AreEqual("spell:bless",action.Effect!.Source);Assert.AreEqual(CombatDuration.Rounds,action.Timer.Duration);
        cut.Find("[data-testid='combat-game-seconds']").Change("12");cut.Find("[data-testid='combat-advance-game-time']").Click();Assert.AreEqual(12,action.Amount);Assert.AreEqual(CombatTransitionKind.AdvanceGameTime,action.Kind);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task QuickDigitalCombatAttackWaitsForArmingAndCanResumeWithoutSpendingAgain()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-combat-ui-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=CombatRules.Transition(SampleWorkspace.Create().Character,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="test",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="turn1"});
            var repo=TestStores.Open(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);
            using var ctx=new BunitContext();ctx.JSInterop.Mode=JSRuntimeMode.Loose;var cut=ctx.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Digital).Add(x=>x.QuickRoll,true));
            var context=CombatRules.Context(session.Character!,"attack:longbow") with{AttackProfile=new(){Name="Longbow",Dice=new(1,8),Modifier=4,DamageType="Piercing"}};
            await cut.InvokeAsync(()=>cut.Instance.OpenRollAsync(new(){Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,CombatContext=context},new(7,[]),new(1,8)));
            Assert.IsNull(session.Character!.Play.LastRoll);Assert.AreEqual(1,cut.FindAll("[data-testid='arm-dreadful-strike']").Count);
            cut.Find("[data-testid='roll-digital']").Click();cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastRoll));Assert.IsNull(session.Error,session.Error);
            var id=session.Character!.Play.LastRoll!.Id;var remaining=session.Character.Combat.AttacksRemaining;await session.ReloadAsync();await cut.InvokeAsync(()=>cut.Instance.ResumeRollAsync());
            Assert.AreEqual(id,session.Character.Play.LastRoll!.Id);Assert.AreEqual(remaining,session.Character.Combat.AttacksRemaining);Assert.AreEqual(1,cut.FindAll("[data-testid='roll-result']").Count);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestMethod]
    public void OptionalModeMakesEngagementAndLockReasonsVisibleAndPreviewsEndTurn()
    {
        using var ctx=new BunitContext();
        var c=CombatRules.Transition(SampleWorkspace.Create().Character,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="test",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="turn1"});
        CombatTransitionRequest? action=null;
        var cut=ctx.Render<CombatWorkspace>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnTransition,r=>action=r));
        StringAssert.Contains(cut.Find("[data-testid='combat-engaged']").TextContent,"COMBAT MODE");
        cut.Find("[data-testid='combat-end-turn']").Click();
        Assert.IsNull(action,"Previewing an end-turn must not commit.");
        StringAssert.Contains(cut.Find("[data-testid='combat-advance-preview']").TextContent,"Action");
        cut.Find("[data-testid='combat-cancel-advance']").Click();Assert.IsNull(action);
        cut.Find("[data-testid='combat-end-turn']").Click();cut.Find("[data-testid='combat-confirm-advance']").Click();
        Assert.AreEqual(CombatTransitionKind.EndTurn,action!.Kind);Assert.IsTrue(action.AdvanceConfirmed);
        cut.Render(p=>p.Add(x=>x.Character,c with{Combat=c.Combat with{ActionAvailable=false}}));
        Assert.IsTrue(cut.Find("[data-combat-action='dodge']").HasAttribute("disabled"));
        StringAssert.Contains(cut.Markup,"Your Action has been spent");
    }
    [TestMethod]
    public void OutsideModeShowsEntryButDoesNotChangeOrdinarySheet()
    {
        using var ctx=new BunitContext();var c=SampleWorkspace.Create().Character;CombatTransitionRequest? action=null;
        var cut=ctx.Render<CombatWorkspace>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnTransition,r=>action=r));
        cut.Find("[data-testid='enter-combat']").Click();Assert.IsNull(action);
        cut.Find("[data-testid='combat-own-turn-now']").Change(true);cut.Find("[data-testid='confirm-enter-combat']").Click();
        Assert.AreEqual(CombatTransitionKind.Enter,action!.Kind);Assert.IsTrue(action.TableBoundaryConfirmed);Assert.IsTrue(action.OwnTurnNow);
    }
}
