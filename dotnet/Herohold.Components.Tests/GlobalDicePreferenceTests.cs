using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class GlobalDicePreferenceTests
{
    private string directory="";
    [TestInitialize] public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-global-dice-"+Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private async Task<WorkspaceSession> Session()
    {var repo=HandMadeStore.At(directory);var c=SampleWorkspace.Create().Character with{Build=null};await repo.SaveAsync(new(){Character=c},0);var s=new WorkspaceSession(repo);await s.SwitchCharacterAsync(c.Id);return s;}
    private static BunitContext Context(WorkspaceSession session)
    {var ctx=new BunitContext();ctx.JSInterop.Mode=JSRuntimeMode.Loose;ctx.Services.AddSingleton(session);return ctx;}
    [TestMethod]
    public async Task GlobalPhysicalFlowsHaveDirectEntryAndNoRepeatedModePickers()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,s.Character!).Add(x=>x.Session,s).Add(x=>x.PreferredDiceMode,DiceMode.Physical).Add(x=>x.QuickRoll,true));var revision=s.Snapshot!.Revision;
        await cut.InvokeAsync(()=>cut.Instance.OpenRollAsync(new(){Name="Wisdom check",Kind=RollKind.Ability,Ability=Ability.Wisdom},new(0,[])));
        Assert.IsNotNull(cut.Find("#physical-d20"));Assert.AreEqual(0,cut.FindAll(".dice-mode").Count,"The header is the only dice-mode control.");Assert.IsNull(s.Character!.Play.LastRoll);Assert.AreEqual(revision,s.Snapshot.Revision);
        await cut.InvokeAsync(()=>cut.Instance.OpenCastAsync(s.Character.Play.Spells.First()));Assert.AreEqual(0,cut.FindAll(".dice-mode").Count);
        await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Short));Assert.IsNotNull(cut.Find("#rest-physical-hit-die"));Assert.AreEqual(0,cut.FindAll(".dice-mode").Count);Assert.AreEqual(revision,s.Snapshot.Revision);
    }
    [TestMethod]
    public async Task MountedDigitalCheckImmediatelyRecordsExactlyOneRollAndOffersOptionsBeforehand()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<Home>();var revision=s.Snapshot!.Revision;
        Assert.IsNotNull(cut.Find("[data-testid='next-roll-options']"));
        await cut.Find("[data-testid='check-wisdom']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(s.Character!.Play.LastRoll));Assert.AreEqual(revision+1,s.Snapshot.Revision);Assert.IsNotNull(cut.Find("[data-testid='roll-result']"));Assert.AreEqual(0,cut.FindAll("#roll-dialog [data-testid='roll-digital']").Count);
    }
    [TestMethod]
    public async Task PendingPhysicalRollKeepsItsModeAfterHeaderPreferenceChangesAndInvalidInputDoesNotSave()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<Home>();cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");await cut.Find("[data-testid='check-wisdom']").ClickAsync(new());var revision=s.Snapshot!.Revision;
        cut.Find("[data-testid='preferred-dice-mode']").Change("Digital");cut.Find("#physical-d20").Change("21");await cut.Find("[data-testid='roll-digital']").ClickAsync(new());Assert.AreEqual(revision,s.Snapshot.Revision);Assert.IsNull(s.Character!.Play.LastRoll);Assert.IsNotNull(cut.Find("#physical-d20"));
        cut.Find("#physical-d20").Change("7");await cut.Find("[data-testid='roll-digital']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(s.Character!.Play.LastRoll));Assert.AreEqual(DiceMode.Physical,s.Character!.Play.LastRoll!.Mode);CollectionAssert.AreEqual(new[]{7},s.Character.Play.LastRoll.D20);
    }
    [TestMethod]
    public async Task NextRollPresetIsChosenBeforeRngCancelKeepsItAndItResetsAfterOneSuccessfulRoll()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<Home>();var revision=s.Snapshot!.Revision;
        await cut.Find("[data-testid='next-roll-options']").ClickAsync(new());cut.Find("#next-roll-flat").Change("101");await cut.Find("[data-testid='save-roll-options']").ClickAsync(new());Assert.AreEqual(revision,s.Snapshot.Revision);Assert.IsNull(s.Character!.Play.LastRoll);StringAssert.Contains(cut.Find("#next-roll-options-dialog").TextContent,"Choose a valid");
        cut.Find("#next-roll-flat").Change("2");cut.Find("#next-roll-advantage").Change("Advantage");cut.Find("#next-roll-bonus-count").Change("1");await cut.Find("[data-testid='save-roll-options']").ClickAsync(new());StringAssert.Contains(cut.Find("[data-testid='next-roll-options']").TextContent,"Advantage");
        await cut.Find("[data-testid='next-roll-options']").ClickAsync(new());cut.Find("#next-roll-advantage").Change("Disadvantage");await cut.Find("[data-testid='cancel-roll-options']").ClickAsync(new());Assert.AreEqual(revision,s.Snapshot.Revision);Assert.IsNull(s.Character.Play.LastRoll);
        await cut.Find("[data-testid='check-wisdom']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(s.Character.Play.LastRoll));var result=s.Character.Play.LastRoll!;Assert.AreEqual(RollAdvantage.Advantage,result.Advantage);Assert.AreEqual(2,result.D20.Count);Assert.AreEqual(2,result.FlatBonus);Assert.AreEqual(1,result.BonusDice.Count);Assert.AreEqual("Roll options",cut.Find("[data-testid='next-roll-options']").TextContent.Trim());
    }
    [TestMethod]
    public async Task PhysicalCastEffectKeepsItsReceiptModeWhenGlobalPreferenceChanges()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<Home>();cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");var dialog=cut.FindComponent<GameplayDialogs>();await dialog.InvokeAsync(()=>dialog.Instance.OpenCastAsync(s.Character!.Play.Spells.Single(x=>x.Id=="cure-wounds")));var revision=s.Snapshot!.Revision;
        cut.Find("[data-testid='preferred-dice-mode']").Change("Digital");Assert.AreEqual(revision,s.Snapshot.Revision);await cut.Find("[data-testid='confirm-cast']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(s.Character!.Play.LastCast));Assert.AreEqual(DiceMode.Physical,s.Character!.Play.LastCast!.Mode);Assert.IsNull(s.Character.Play.LastCast.EffectTotal);
        cut.Find("#cast-effect-physical").Change("2,3");await cut.Find("[data-testid='roll-cast-effect']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(s.Character.Play.LastCast!.EffectTotal));CollectionAssert.AreEqual(new[]{2,3},s.Character.Play.LastCast!.EffectDice);
    }
    [TestMethod]
    public async Task LevelUpStartsWithClassSelectionAndUnavailableClassesExplainPermanentScoreRequirements()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<WorkspaceDialogs>(p=>p.Add(x=>x.Character,s.Character!).Add(x=>x.Session,s));var revision=s.Snapshot!.Revision;
        await cut.Find("[data-testid='open-level-up']").ClickAsync(new());Assert.IsNull(s.Workflow);Assert.AreEqual(revision,s.Snapshot.Revision);var options=cut.Find("[data-testid='level-class-options']");Assert.AreEqual(12,options.QuerySelectorAll("input").Length);Assert.IsTrue(options.QuerySelector("input[value='Wizard']")!.HasAttribute("disabled"));Assert.IsFalse(options.QuerySelector("input[value='Fighter']")!.HasAttribute("disabled"));StringAssert.Contains(options.TextContent,"requires13");
    }
    [TestMethod]
    public async Task PhysicalRestStagesOnlyEnteredDiceEvenWhenHeaderChangesAndCancelDoesNotSave()
    {
        var s=await Session();using var ctx=Context(s);var cut=ctx.Render<Home>();cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");var dialog=cut.FindComponent<GameplayDialogs>();await dialog.InvokeAsync(()=>dialog.Instance.OpenRestAsync(RestKind.Short));var revision=s.Snapshot!.Revision;var hp=s.Character!.CurrentHp;
        cut.Find("[data-testid='preferred-dice-mode']").Change("Digital");cut.Find("#rest-physical-hit-die").Change("21");await cut.Find("[data-testid='stage-hit-die']").ClickAsync(new());Assert.AreEqual(revision,s.Snapshot.Revision);StringAssert.Contains(cut.Find("#rest-dialog").TextContent,"Selected dice: none");
        cut.Find("#rest-physical-hit-die").Change("7");await cut.Find("[data-testid='stage-hit-die']").ClickAsync(new());StringAssert.Contains(cut.Find("#rest-dialog").TextContent,"Selected dice: 7");await cut.Find("#rest-dialog .form-footer button").ClickAsync(new());Assert.AreEqual(revision,s.Snapshot.Revision);Assert.AreEqual(hp,s.Character.CurrentHp);
    }
}
