using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class NativeSheetTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-native-sheet-"+Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private async Task<WorkspaceSession> Session(CharacterState? character=null)
    {
        var c=(character??SampleWorkspace.Create().Character) with{Id="native-sheet",Build=character?.Build};var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);return session;
    }
    private static BunitContext Context(WorkspaceSession session)
    {var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);return context;}
    [TestMethod]
    public async Task MountedAbilityScoresModifiersAndChecksUseActualCharacterDataWithoutSpendingAnythingOnRead()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
        foreach(var ability in Enum.GetValues<Ability>())
        {
            var row=cut.Find($"[data-testid='ability-{ability.ToString().ToLowerInvariant()}']");StringAssert.Contains(row.QuerySelector(".sheet-ability-score")!.TextContent,session.Character!.AbilityScores[ability].ToString());
            Assert.AreEqual(CharacterCalculations.Ability(session.Character,ability).Total.ToString("+0;-0;+0"),row.QuerySelector(".calculation-trigger")!.TextContent.Trim());
        }
        StringAssert.Contains(cut.Find("[data-testid='sheet-proficiency']").TextContent,CharacterCalculations.ProficiencyBonus(session.Character!.Level).ToString());Assert.AreEqual(revision,session.Snapshot.Revision);
        cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");await cut.Find("[data-testid='check-wisdom']").ClickAsync(new());StringAssert.Contains(cut.Find("#roll-title").TextContent,"Wisdom check");Assert.IsNull(session.Character.Play.LastRoll);Assert.AreEqual(revision,session.Snapshot.Revision);
        cut.Find("#physical-d20").Change("14");await cut.Find("[data-testid='roll-digital']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastRoll));Assert.AreEqual(14+CharacterCalculations.Ability(session.Character!,Ability.Wisdom).Total,session.Character!.Play.LastRoll!.Total);
    }
    [TestMethod]
    public async Task ProfileChangesAreExplicitAndPreserveBuildSources()
    {
        var c=SampleWorkspace.Create().Character with{Build=new(){Background="Sage"}};var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
        await cut.Find("[data-testid='open-profile']").ClickAsync(new());Assert.AreEqual("Sage",cut.Find("#profile-background").GetAttribute("value"));Assert.AreEqual(revision,session.Snapshot.Revision);
        cut.Find("#profile-alignment").Input("Neutral Good");cut.Find("#profile-backstory").Input("A scholar seeking a lost observatory.");cut.Find("#profile-xp").Input("12345");
        await cut.Find("[data-testid='save-profile']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual("Neutral Good",SheetRules.Profile(session.Character!).Alignment));
        Assert.AreEqual(12345,SheetRules.ExperiencePoints(session.Character!));Assert.AreEqual("Sage",session.Character!.Build!.Background);
        await session.ReloadAsync();Assert.AreEqual("A scholar seeking a lost observatory.",SheetRules.Profile(session.Character!).Backstory);Assert.IsNull(session.Character!.Sheet.Profile.Background,"Saving an unrelated field must not turn a derived background into a manual override.");
    }
    [TestMethod]
    public async Task PurseReceivesAndSpendsIndependentDenominationsWithoutPartialInsufficientTransactions()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
        await cut.Find("[data-testid='open-purse']").ClickAsync(new());Assert.AreEqual(5,cut.FindAll("#purse-dialog input[type='number']").Count);Assert.AreEqual(revision,session.Snapshot.Revision);
        cut.Find("#coin-gp").Input("5");cut.Find("#coin-sp").Input("2");await cut.Find("[data-testid='apply-coins']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual(5,SheetRules.Purse(session.Character!).Gp));Assert.AreEqual(2,SheetRules.Purse(session.Character!).Sp);
        cut.Find("#coin-change-kind").Change("Spend");cut.Find("#coin-gp").Input("6");cut.Find("#coin-sp").Input("1");await cut.Find("[data-testid='apply-coins']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsNotNull(session.Error));Assert.AreEqual(5,SheetRules.Purse(session.Character!).Gp);Assert.AreEqual(2,SheetRules.Purse(session.Character!).Sp);
        cut.Find("#coin-gp").Input("3");await cut.Find("[data-testid='apply-coins']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual(2,SheetRules.Purse(session.Character!).Gp));await session.ReloadAsync();Assert.AreEqual(1,SheetRules.Purse(session.Character!).Sp);
    }
    [TestMethod]
    public async Task SurvivalRecordsInspirationAndPhysicalDeathSavesWithExplicitCorrectionAndReload()
    {
        var session=await Session(SampleWorkspace.Create().Character with{CurrentHp=0});using var context=Context(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
        cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");await cut.Find("[data-testid='open-survival']").ClickAsync(new());Assert.AreEqual(revision,session.Snapshot.Revision);cut.Find("#heroic-inspiration").Change(true);cut.WaitForAssertion(()=>Assert.IsTrue(SheetRules.HasHeroicInspiration(session.Character!)));
        Assert.AreEqual(0,cut.FindAll(".death-dice-mode").Count,"Death saves use the header preference without another picker.");
        cut.Find("[data-testid='preferred-dice-mode']").Change("Digital");
        cut.Find("#death-save-d20").Input("1");await cut.Find("[data-testid='record-death-save']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual(2,session.Character!.Sheet.DeathSaves.Failures));
        cut.Find("#death-successes").Input("0");cut.Find("#death-failures").Input("0");cut.Find("#death-stable").Change(true);await cut.Find("[data-testid='correct-death-saves']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.IsTrue(session.Character!.Sheet.DeathSaves.Stable));
        await session.ReloadAsync();Assert.IsTrue(SheetRules.HasHeroicInspiration(session.Character!));Assert.AreEqual(0,session.Character!.CurrentHp);Assert.IsTrue(session.Character.Sheet.DeathSaves.Stable);
    }
    [TestMethod]
    [DataRow(false,1)]
    [DataRow(true,2)]
    public async Task DamageAtZeroHpCanRecordNormalOrCriticalDeathSaveFailures(bool critical,int failures)
    {
        var session=await Session(SampleWorkspace.Create().Character with{CurrentHp=0,TemporaryHp=0});using var context=Context(session);var cut=context.Render<Home>();await cut.Find("[data-testid='quick-damage']").ClickAsync(new());
        cut.Find("#health-amount").Input("1");cut.Find("#health-critical-damage").Change(critical);Assert.IsFalse(cut.Find("[data-testid='confirm-health']").HasAttribute("disabled"));
        await cut.Find("[data-testid='confirm-health']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual(failures,session.Character!.Sheet.DeathSaves.Failures));Assert.AreEqual(0,session.Character!.CurrentHp);Assert.AreEqual(1,session.Character.Play.UndoHistory.Count);
    }
    [TestMethod]
    public async Task TrainingExpertiseAndOwnedReferencesKeepClassFeatsAndSpeciesSourcesSeparate()
    {
        var c=SampleWorkspace.Create().Character with{Level=9,SkillProficiencies=["Insight"],Expertise=["Insight"],SaveProficiencies=[Ability.Strength],Features=["Homebrew ribbon"],Derived=new(){Size="Medium",Languages=["Common","Elvish"],ArmorProficiencies=["Light","Shields"],WeaponProficiencies=["Simple"],ToolProficiencies=["Navigator's tools"],FeatureDetails=[new(){Name="Favored Enemy",Description="Always prepared mark.",Source="class:Ranger"},new(){Name="Lucky",Description="Luck points.",Source="origin:background:Sage"},new(){Name="Fey Ancestry",Description="Fey protection.",Source="species:Elf"}]}};
        var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();StringAssert.Contains(cut.Find("[data-testid='training-insight']").TextContent,"Expertise");
        await cut.Find("[data-testid='open-references']").ClickAsync(new());StringAssert.Contains(cut.Find("#sheet-references-dialog").TextContent,"Navigator's tools");StringAssert.Contains(cut.Find("#sheet-references-dialog").TextContent,"Elvish");
        foreach(var (group,name) in new[]{("class","Favored Enemy"),("feats","Lucky"),("species","Fey Ancestry"),("other","Homebrew ribbon")})StringAssert.Contains(cut.Find($"[data-testid='features-{group}']").TextContent,name);
        await cut.Find("[aria-label='Close training and features']").ClickAsync(new());Assert.AreEqual("Proficient",cut.Find("[data-testid='save-training-strength']").TextContent.Trim());Assert.AreEqual("Untrained",cut.Find("[data-testid='save-training-dexterity']").TextContent.Trim());
    }
    [TestMethod]
    public async Task EditingProfileWithoutTouchingXpPreservesAnUnrepresentableImportedXpValue()
    {
        var c=SampleWorkspace.Create().Character with{Build=new(){PdfPassthroughFields=new(){["XP"]=new(){Text="milestone"}}}};var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();await cut.Find("[data-testid='open-profile']").ClickAsync(new());cut.Find("#profile-alignment").Input("Neutral");await cut.Find("[data-testid='save-profile']").ClickAsync(new());
        Assert.IsNull(session.Character!.Sheet.ExperiencePoints);Assert.AreEqual("milestone",session.Character.Build!.PdfPassthroughFields!["XP"].Text);
    }
    [TestMethod]
    public async Task SettingOnePurseBalanceStartsWithCurrentAmountsAndKeepsOtherDenominations()
    {
        var c=SampleWorkspace.Create().Character with{Sheet=new(){Coins=new(){Gp=7,Sp=2}}};var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();await cut.Find("[data-testid='open-purse']").ClickAsync(new());cut.Find("#coin-change-kind").Change("Set");Assert.AreEqual("7",cut.Find("#coin-gp").GetAttribute("value"));Assert.AreEqual("2",cut.Find("#coin-sp").GetAttribute("value"));cut.Find("#coin-gp").Input("9");await cut.Find("[data-testid='apply-coins']").ClickAsync(new());Assert.AreEqual(9,SheetRules.Purse(session.Character!).Gp);Assert.AreEqual(2,SheetRules.Purse(session.Character!).Sp);
    }
    [TestMethod]
    public async Task DeadCharactersHealingPreviewExplainsWhyManualCorrectionIsRequired()
    {
        var c=SampleWorkspace.Create().Character with{CurrentHp=0,Sheet=new(){DeathSaves=new(){Failures=3,Dead=true}}};var session=await Session(c);using var context=Context(session);var cut=context.Render<Home>();await cut.Find("[data-testid='quick-heal']").ClickAsync(new());cut.Find("#health-amount").Input("10");StringAssert.Contains(cut.Find("[data-testid='health-preview-error']").TextContent.ToLowerInvariant(),"dead");Assert.IsTrue(cut.Find("[data-testid='confirm-health']").HasAttribute("disabled"));Assert.AreEqual(0,session.Character!.CurrentHp);
    }

    [TestMethod]
    [DataRow(CombatPhase.BetweenTurns, false)]
    [DataRow(CombatPhase.OwnTurn, true)]
    public async Task CombatDeathSaveButtonLocksBeforeDigitalDiceWhenOffTurnOrAlreadyRecorded(CombatPhase phase,bool recorded)
    {
        var c=SampleWorkspace.Create().Character with{CurrentHp=0,Combat=new(){Enabled=true,EncounterActive=true,EncounterId="fight",TurnId="own-turn",Phase=phase,DeathSaveRecordedTurnId=recorded?"own-turn":""}};
        var session=await Session(c);using var context=Context(session);var cut=context.Render<Herohold.Web.Components.Shared.SurvivalDialog>(p=>p.Add(x=>x.Session,session));await cut.InvokeAsync(cut.Instance.OpenAsync);
        Assert.IsTrue(cut.Find("[data-testid='record-death-save']").HasAttribute("disabled"));
    }
}
