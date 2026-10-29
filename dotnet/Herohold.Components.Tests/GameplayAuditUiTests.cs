using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class GameplayAuditUiTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-gameplay-ui",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private async Task<WorkspaceSession> Session(CharacterState? character=null)
    {
        var repository=HandMadeStore.At(directory);var c=(character??SampleWorkspace.Create().Character) with{Id="fictional-gameplay-ui",Build=character?.Build};
        await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);Assert.IsNull(session.Error);return session;
    }
    private static BunitContext Context(WorkspaceSession session)
    {var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);return context;}
    private static IRenderedComponent<GameplayDialogs> Dialogs(BunitContext context,WorkspaceSession session)=>context.Render<GameplayDialogs>(p=>p.Add(c=>c.Character,session.Character!).Add(c=>c.Session,session).Add(c=>c.PreferredDiceMode,DiceMode.Physical));
    private static async Task Roll(IRenderedComponent<GameplayDialogs> cut,int die)
    {
        await cut.InvokeAsync(()=>cut.Instance.OpenRollAsync(new(){Name="Fictional weapon attack",Kind=RollKind.Attack},new(0,[])));
        cut.Find("#physical-d20").Change(die.ToString());cut.Find("[data-testid='roll-digital']").Click();
        cut.WaitForAssertion(()=>Assert.AreEqual(1,cut.FindAll("[data-testid='roll-result']").Count));
    }
    [TestMethod]public async Task NaturalAttackResultsDisableOnlyTheContradictoryOutcome()
    {
        var session=await Session();using var context=Context(session);var cut=Dialogs(context,session);
        foreach(var die in new[]{1,20})
        {
            await Roll(cut,die);
            Assert.AreEqual(die==1,cut.Find("[data-testid='attack-hit']").HasAttribute("disabled"));Assert.AreEqual(die==20,cut.Find("[data-testid='attack-miss']").HasAttribute("disabled"));
        }
    }
    [TestMethod][DataRow(10,"1d6","2d6")][DataRow(11,"1d6","2d8")][DataRow(20,"1d10","2d8")]
    public async Task DamageEffectLabelsUseActualHighLevelRangerDice(int rangerLevel,string markDice,string dreadDice)
    {
        var build=SampleWorkspace.Create().Character.Build! with{Level=20,ClassLevels=new(){["Ranger"]=rangerLevel,["Fighter"]=20-rangerLevel}};
        var session=await Session(SampleWorkspace.Create().Character with{Level=20,Build=build});using var context=Context(session);var cut=Dialogs(context,session);await Roll(cut,12);
        StringAssert.Contains(cut.Find("#effect-hm").ParentElement!.TextContent,markDice);StringAssert.Contains(cut.Find("#effect-dread").ParentElement!.TextContent,dreadDice);
    }
    [TestMethod]public async Task LongCastingWarnsAboutTheCurrentConcentrationBeforeAnyPayment()
    {
        var baseCharacter=SampleWorkspace.Create().Character;var alarm=new SpellDefinition{Id="fictional-alarm",Name="Alarm",Level=1,CastingTime="1 minute",Ritual=true};
        var session=await Session(baseCharacter with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=baseCharacter.Play with{Spells=[..baseCharacter.Play.Spells,alarm],GrantedSpellIds=[..baseCharacter.Play.GrantedSpellIds,alarm.Id],HunterTarget="Old quarry"}});
        using var context=Context(session);var cut=Dialogs(context,session);var revision=session.Snapshot!.Revision;await cut.InvokeAsync(()=>cut.Instance.OpenCastAsync(alarm));
        StringAssert.Contains(cut.Find("[data-testid='casting-concentration-warning']").TextContent,"Hunter’s Mark");Assert.AreEqual(revision,session.Snapshot.Revision);
        cut.Find("#cast-ritual").Change(true);cut.Find("[data-testid='confirm-cast']").Click();
        cut.WaitForAssertion(()=>Assert.IsFalse(session.Character!.Effects.Any(effect=>effect.Concentration)));Assert.AreEqual("",session.Character!.Play.HunterTarget);
    }
    [TestMethod]public async Task ZeroHpRestCannotStageDiceOrConfirmAndExplainsRecovery()
    {
        var session=await Session(SampleWorkspace.Create().Character with{CurrentHp=0});using var context=Context(session);var cut=Dialogs(context,session);await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Short));
        Assert.IsTrue(cut.Find("[data-testid='stage-hit-die']").HasAttribute("disabled"));Assert.IsTrue(cut.Find("[data-testid='confirm-rest']").HasAttribute("disabled"));
        StringAssert.Contains(cut.Find("#rest-dialog [role='alert']").TextContent,"1 Hit Point");
    }
    [TestMethod]public async Task HomeShowsNativeUnarmedActionWithoutAnInventoryItemAndDisablesZeroHpRests()
    {
        var c=SampleWorkspace.Create().Character with{ClassName="Monk",Subclass="",CurrentHp=0,Equipment=[],Derived=new(){Attacks=[new(){Id="unarmed",Name="Unarmed Strike",Kind="unarmed",Ability=Ability.Dexterity,Proficient=true,DamageDice=new(1,8),DamageModifier=4,DamageType="Bludgeoning"},new(){Id="weapon:longbow",Name="Longbow",Ability=Ability.Dexterity,DamageDice=new(1,8)}]}};
        var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();
        Assert.AreEqual(1,cut.FindAll("[data-testid='roll-unarmed']").Count);Assert.AreEqual(0,cut.FindAll("[data-testid='roll-longbow']").Count);
        Assert.IsTrue(cut.Find("[data-testid='short-rest']").HasAttribute("disabled"));Assert.IsTrue(cut.Find("[data-testid='long-rest']").HasAttribute("disabled"));
    }
    [TestMethod]public async Task MovingMarkRequiresDefeatConfirmationAndPersistsWithoutSpendingAResource()
    {
        var c=SampleWorkspace.Create().Character;c=c with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=c.Play with{HunterTarget="First quarry"}};
        var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();var uses=session.Character!.Resources.ToDictionary(pool=>pool.Id,pool=>pool.Remaining);
        cut.Find("[data-testid='move-mark']").Click();cut.Find("#move-mark-target").Input("Next quarry");Assert.IsTrue(cut.Find("[data-testid='confirm-move-mark']").HasAttribute("disabled"));
        cut.Find("#move-mark-defeated").Change(true);cut.Find("[data-testid='confirm-move-mark']").Click();cut.WaitForAssertion(()=>Assert.AreEqual("Next quarry",session.Character!.Play.HunterTarget));
        foreach(var pool in session.Character!.Resources)Assert.AreEqual(uses[pool.Id],pool.Remaining);await session.ReloadAsync();Assert.AreEqual("Next quarry",session.Character!.Play.HunterTarget);
    }
    [TestMethod]public async Task QueuedMarkTransferCannotAffectAnotherCharacterAtTheSameRevision()
    {
        var c=SampleWorkspace.Create().Character;c=c with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=c.Play with{HunterTarget="First quarry"}};
        var session=await Session(c);using var context=Context(session);var cut=Dialogs(context,session);await cut.InvokeAsync(()=>cut.Instance.OpenMoveMarkAsync());
        cut.Find("#move-mark-target").Input("Stale target");cut.Find("#move-mark-defeated").Change(true);
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c with{Id="second-fictional-character",Play=c.Play with{HunterTarget="Second quarry"}}},0);
        await session.SwitchCharacterAsync("second-fictional-character");cut.Find("[data-testid='confirm-move-mark']").Click();
        cut.WaitForAssertion(()=>Assert.IsNotNull(session.Error));Assert.AreEqual("Second quarry",session.Character!.Play.HunterTarget);Assert.AreEqual(1L,session.Snapshot!.Revision);StringAssert.Contains(session.Error!,"another character");
    }
}
