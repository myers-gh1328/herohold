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
public sealed class PlayerLayoutTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-player-layout-"+Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private async Task<WorkspaceSession> Session()
    {
        var repo=TestStores.Open(directory);await repo.SaveAsync(SampleWorkspace.Create(),0);
        var session=new WorkspaceSession(repo);await session.InitializeAsync();return session;
    }
    private static BunitContext Context(WorkspaceSession session)
    {var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);return context;}

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("adventure")][DataRow("equipment")][DataRow("journal")]
    public async Task EveryCharacterViewHasOneHeaderAndDirectlyAvailableResources(string view)
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        await cut.Find($"[data-testid='nav-{view}']").ClickAsync(new());
        Assert.AreEqual(1,cut.FindAll("header#character-context").Count);
        Assert.AreEqual(0,cut.FindAll(".prototype-banner,.topbar").Count,"A player should not pass three stacked headers.");
        var counters=cut.Find("[data-testid='resource-strip']");
        foreach(var pool in session.Character!.Resources)
        {
            // Spell slots sit once in the Magic panel on Play; every other pool stays in the rail on every view.
            var slot=MagicPanel.SlotLevel(pool) is >=1 and <=9;if(slot&&view!="adventure"){Assert.IsNull(counters.QuerySelector($"[data-testid='resource-{pool.Id}']"),pool.Name);continue;}
            var owner=slot?cut.Find("[data-testid='adventure-magic']"):counters;
            Assert.IsNotNull(owner.QuerySelector($"[data-testid='resource-{pool.Id}'],[data-testid='magic-slot-{pool.Id}']"),pool.Name);
            Assert.IsNotNull(owner.QuerySelector($"button[aria-label=\"Use one {pool.Name}\"]"),pool.Name);
        }
        Assert.AreEqual(0,cut.FindAll("details#resource-tools").Count,"Counters must not depend on a hidden tools section.");
        Assert.IsNotNull(cut.Find("[data-testid='character-menu'] [data-testid='export-json']"));
        Assert.AreEqual(0,cut.FindAll("header .top-actions").Count);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ResourceCanBeSpentFromJournalWithoutNavigationAndSurvivesReload()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        await cut.Find("[data-testid='nav-journal']").ClickAsync(new());var before=session.Character!.Resources.Single(r=>r.Id=="luck").Remaining;
        await cut.Find("[data-testid='resource-strip'] button[aria-label='Use one Luck points']").ClickAsync(new());
        cut.WaitForAssertion(()=>Assert.AreEqual(before-1,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining));
        StringAssert.Contains(cut.Find("#journal-sheet-title").TextContent,"Journal");await session.ReloadAsync();Assert.AreEqual(before-1,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("adventure")]
    public async Task PassiveReferencesAreVisibleAndMatchActualSkillTotals(string view)
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();await cut.Find($"[data-testid='nav-{view}']").ClickAsync(new());
        foreach(var (name,ability) in new[]{("Perception",Ability.Wisdom),("Insight",Ability.Wisdom),("Investigation",Ability.Intelligence)})
            StringAssert.Contains(cut.Find("[data-testid='passive-"+name.ToLowerInvariant()+"']").TextContent,(10+CharacterCalculations.Skill(session.Character!,name,ability).Total).ToString());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AdventurePutsActiveAndPassiveNumbersInOneRowAndOpensTheRealRoll()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        var row=cut.Find("[data-testid='skill-perception']");
        StringAssert.Contains(row.TextContent,"Perception");
        StringAssert.Contains(row.QuerySelector("[data-testid='active-perception']")!.TextContent,CharacterCalculations.Skill(session.Character!,"Perception",Ability.Wisdom).Total.ToString());
        StringAssert.Contains(row.QuerySelector("[data-testid='passive-perception']")!.TextContent,(10+CharacterCalculations.Skill(session.Character!,"Perception",Ability.Wisdom).Total).ToString());
        Assert.AreEqual(0,cut.FindAll("main>.passive-scores").Count);
        await cut.Find("[data-testid='roll-skill-perception']").ClickAsync(new());
        StringAssert.Contains(cut.Find("#roll-dialog").TextContent,"Perception check");
        Assert.AreEqual(0,cut.FindAll("#roll-dialog .dice-mode").Count);
        Assert.IsNotNull(session.Character!.Play.LastRoll);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow(0,false,"No conditions")][DataRow(1,false,"Exhaustion 1")]
    [DataRow(1,true,"Poisoned · Exhaustion 1")][DataRow(0,true,"Poisoned")]
    public async Task ConditionSummaryTreatsExhaustionAsAnActiveCondition(int exhaustion,bool poisoned,string expected)
    {
        var session=await Session();if(poisoned)await session.AddEffectAsync("Poisoned","Trap","",null,false);await session.SetExhaustionAsync(exhaustion);
        using var context=Context(session);var cut=context.Render<Home>();
        Assert.AreEqual(expected,cut.Find("header [data-testid='manage-effects']").TextContent.Trim());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AllSkillsStayVisibleOnFirstLoadAfterNavigationAndReload()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        Assert.AreEqual(18,cut.FindAll(".adventure-skill").Count);
        Assert.AreEqual(0,cut.FindAll(".adventure-checks button").Count(b=>b.TextContent.Contains("All 18 skills")||b.TextContent.Contains("Quick checks")));
        foreach(var skill in new[]{"arcana","animal-handling","sleight-of-hand"})Assert.IsNotNull(cut.Find("[data-testid='roll-skill-"+skill+"']"));
        await cut.Find("[data-testid='nav-journal']").ClickAsync(new());await cut.Find("[data-testid='nav-adventure']").ClickAsync(new());
        Assert.AreEqual(18,cut.FindAll(".adventure-skill").Count);
        await session.ReloadAsync();cut.WaitForAssertion(()=>Assert.AreEqual(18,cut.FindAll(".adventure-skill").Count));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ApplicationDrawerKeepsJournalInDialogAndInventoryInTheSheet()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        Assert.IsNotNull(cut.Find("button[data-testid='open-app-drawer']"));
        Assert.IsNotNull(cut.Find("dialog#app-drawer [data-testid='theme']"));
        Assert.AreEqual(0,cut.FindAll("aside.sidebar").Count);
        await cut.Find("[data-testid='nav-equipment']").ClickAsync(new());
        Assert.IsNotNull(cut.Find(".inventory-view [data-testid='inventory-weight']"));Assert.AreEqual(0,cut.FindAll("dialog#inventory-sheet-dialog").Count);
        await cut.Find("[data-testid='nav-journal']").ClickAsync(new());Assert.IsNotNull(cut.Find("dialog#journal-sheet-dialog"));
        Assert.IsTrue(cut.Find("#health-emblem").TextContent.Contains(session.Character!.CurrentHp.ToString()));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task SavingThrowsAreAvailableWithoutEnteringCombatAndUseGlobalDiceMode()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        Assert.AreEqual(6,cut.FindAll(".ability-save").Count);
        cut.Find("[data-testid='preferred-dice-mode']").Change("Physical");await cut.Find("[data-testid='sheet-save-wisdom']").ClickAsync(new());
        StringAssert.Contains(cut.Find("#roll-dialog").TextContent,"Wisdom saving throw");Assert.IsNotNull(cut.Find("#physical-d20"));Assert.IsNull(session.Character!.Play.LastRoll);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task UnifiedPlayKeepsAttacksSkillsAndMagicTogetherWithoutDuplicateModePages()
    {
        var session=await Session();using var context=Context(session);var cut=context.Render<Home>();
        Assert.AreEqual(0,cut.FindAll("[data-testid='nav-combat'],[data-testid='nav-spells']").Count,"Combat is an optional mode on the same Play sheet.");
        Assert.IsNotNull(cut.Find("[data-testid='roll-longbow']"));Assert.AreEqual(18,cut.FindAll(".adventure-skill").Count);Assert.IsNotNull(cut.Find("[data-testid='adventure-magic']"));
        await cut.Find("[data-testid='open-spell-library']").ClickAsync(new());Assert.IsNotNull(cut.Find("dialog#spell-library-dialog .spell-reference-row"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task CharacterLibraryRemainsUsableDuringCombatAndSwitchingPreservesSpentAllowances()
    {
        var session=await Session();var first=session.Character!.Id;var repo=TestStores.Open(directory);
        await repo.SaveAsync(new(){Character=session.Character with{Id="other-player",Name="Other hero",Combat=new()}},0);
        await session.TransitionCombatAsync(new(){OperationId="library-enter",SourceCharacterId=first,SourceRevision=session.Snapshot!.Revision,Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="library-fight",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="first-turn"});
        await session.TransitionCombatAsync(new(){OperationId="library-dash",SourceCharacterId=first,SourceRevision=session.Snapshot!.Revision,Kind=CombatTransitionKind.SpendAction,EncounterId="library-fight",ActionId="dash"});Assert.IsNull(session.Error);
        using var context=Context(session);var cut=context.Render<Home>();await cut.Find("[data-testid='open-app-drawer']").ClickAsync(new());await cut.FindAll("#app-drawer button").Single(b=>b.TextContent.Trim()=="Character library").ClickAsync(new());
        Assert.IsNotNull(cut.Find(".library-toolbar"));await cut.Find("[data-character-id='other-player']").ClickAsync(new());Assert.AreEqual("other-player",session.Character!.Id);
        var stored=await repo.LoadAsync(first);Assert.IsTrue(stored!.Character.Combat.Enabled);Assert.IsFalse(stored.Character.Combat.ActionAvailable);Assert.AreEqual(30,stored.Character.Combat.MovementGranted);
    }
}
