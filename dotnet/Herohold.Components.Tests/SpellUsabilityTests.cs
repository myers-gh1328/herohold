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
public sealed class SpellUsabilityTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-spell-usability-"+Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private async Task<WorkspaceSession> CreateSession()
    {
        var healing=SpellCatalog.All.Single(s=>s.Id=="cure-wounds");
        var c=SampleWorkspace.Create().Character with{Id="spell-usability",Build=null,Resources=[new(){Id="slot1",Name="First-level slots",Maximum=3,Remaining=3,SlotLevel=1}],Play=new(){Spells=[healing],PreparedSpellIds=[healing.Id],KnownSpellIds=[healing.Id]}};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);Assert.IsNull(session.Error);return session;
    }
    private static BunitContext Context(WorkspaceSession session)
    {
        var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session).AddSingleton(new HeroholdFeatures(Combat:true));return context;
    }
    [TestMethod]
    [DataRow(6)]
    [DataRow(8)]
    [DataRow(10)]
    [DataRow(12)]
    public async Task PhysicalShortRestLabelsTheCharactersActualHitDie(int sides)
    {
        var c=SampleWorkspace.Create().Character with{Id="physical-rest-label",Build=null,Play=new(){HitDieSize=sides}};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
        using var context=Context(session);var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
        await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Short));
        var input=cut.Find("#rest-physical-hit-die");Assert.AreEqual($"Unmodified D{sides} result",input.ParentElement!.TextContent.Trim());Assert.AreEqual(sides.ToString(),input.GetAttribute("max"));Assert.AreEqual(32,session.Character!.CurrentHp);Assert.AreEqual(1L,session.Snapshot!.Revision);
    }
    [TestMethod]
    public async Task ReadingASpellThenCastingFromItsDetailsOpensNormalReviewWithoutSpendingAnything()
    {
        var session=await CreateSession();using var context=Context(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
        cut.Find("[data-testid='open-spell-library']").Click();cut.Find(".spell-reference-row .roll-row-action").Click();
        cut.WaitForAssertion(()=>Assert.AreEqual("Cure Wounds",cut.Find("#spell-reference-title").TextContent));
        Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(3,session.Character!.Resources.Single().Remaining);Assert.IsNull(session.Character.Play.LastCast);
        cut.Find("[data-testid='cast-inspected-spell']").Click();
        cut.WaitForAssertion(()=>Assert.AreEqual("Cure Wounds",cut.Find("#cast-title").TextContent));
        Assert.AreEqual("slot1",cut.Find("#cast-payment option").GetAttribute("value"));Assert.IsFalse(cut.Find("[data-testid='confirm-cast']").HasAttribute("disabled"));
        Assert.IsTrue(context.JSInterop.Invocations.Any(i=>i.Identifier=="heroholdUi.closeDialog"&&Equals(i.Arguments[0],"spell-reference-dialog")));
        Assert.IsTrue(context.JSInterop.Invocations.Any(i=>i.Identifier=="heroholdUi.openDialog"&&Equals(i.Arguments[0],"spell-cast-dialog")));
        Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(3,session.Character.Resources.Single().Remaining);Assert.IsNull(session.Character.Play.LastCast);Assert.AreEqual(32,session.Character.CurrentHp);
    }
    [TestMethod]
    public async Task HealingResultRequestsExplicitSelfHealingWithRolledAmountWithoutHealingOrPayingAgain()
    {
        var session=await CreateSession();using var context=Context(session);var requested=new List<int>();var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.OnHealingRequested,amount=>requested.Add(amount)).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
        await cut.InvokeAsync(()=>cut.Instance.OpenCastAsync(session.Character!.Play.Spells.Single()));
        Assert.AreEqual(0,cut.FindAll("[data-testid='apply-healing-to-self']").Count);
        cut.Find("[data-testid='confirm-cast']").Click();cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastCast));
        Assert.IsNull(session.Error);Assert.AreEqual(2,session.Character!.Resources.Single().Remaining);Assert.AreEqual(32,session.Character.CurrentHp);Assert.AreEqual(0,requested.Count);
        cut.Find("#cast-effect-physical").Change("2, 3");cut.Find("[data-testid='roll-cast-effect']").Click();
        cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastCast!.EffectTotal));var expected=5+session.Character!.Play.LastCast!.Preview.Modifier;Assert.AreEqual(expected,session.Character.Play.LastCast.EffectTotal);
        Assert.AreEqual(32,session.Character.CurrentHp);Assert.AreEqual(0,requested.Count);Assert.AreEqual(2,session.Character.Resources.Single().Remaining);var revision=session.Snapshot!.Revision;
        cut.Find("[data-testid='apply-healing-to-self']").Click();cut.WaitForAssertion(()=>CollectionAssert.AreEqual(new[]{expected},requested));
        var close=context.JSInterop.Invocations.Last(i=>i.Identifier=="heroholdUi.closeDialog");Assert.AreEqual("spell-cast-dialog",close.Arguments[0],"The casting modal must close before the separate health review opens.");
        Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(32,session.Character.CurrentHp);Assert.AreEqual(2,session.Character.Resources.Single().Remaining);
        await session.ReloadAsync();Assert.AreEqual(32,session.Character!.CurrentHp);Assert.AreEqual(2,session.Character.Resources.Single().Remaining);Assert.AreEqual(expected,session.Character.Play.LastCast!.EffectTotal);
    }
    private async Task<WorkspaceSession> MagicSession(bool legacySlots=false)
    {
        var cure=SpellCatalog.All.Single(s=>s.Id=="cure-wounds");var mark=SpellCatalog.All.Single(s=>s.Id=="hunters-mark");
        var spells=new[]{cure,mark,new SpellDefinition{Id="fictional-light",Name="Fictional Light",Level=0},new SpellDefinition{Id="fictional-blast",Name="Fictional Blast",Level=2},new SpellDefinition{Id="unprepared-book",Name="Unprepared book spell",Level=1}};
        var sample=SampleWorkspace.Create().Character;
        var c=sample with{Id="magic-layout",Build=null,Resources=[new(){Id="slot1",Name="Level 1 slots",SlotLevel=legacySlots?null:1,Maximum=3,Remaining=2},new(){Id="slot2",Name="Level 2 slots",SlotLevel=legacySlots?null:2,Maximum=2,Remaining=1},new(){Id="pact",Name="Pact slots",SlotLevel=3,Maximum=1,Remaining=0,Recovery=RecoveryRest.Short}],Play=new(){Spells=[..spells],PreparedSpellIds=[cure.Id,mark.Id,"fictional-light","fictional-blast"],SpellbookIds=["unprepared-book"]}};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);Assert.IsNull(session.Error);return session;
    }
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task AdventureShowsEveryPreparedSpellWithAllSlotPoolsAndSeparateStatusCards(bool legacySlots)
    {
        var session=await MagicSession(legacySlots);using var context=Context(session);var cut=context.Render<Home>();var magic=cut.Find("[data-testid='adventure-magic']");
        foreach(var id in session.Character!.Play.PreparedSpellIds)Assert.IsNotNull(magic.QuerySelector("[data-testid='cast-"+id+"']"),id);
        Assert.IsNull(magic.QuerySelector("[data-testid='cast-unprepared-book']"));
        foreach(var id in new[]{"slot1","slot2","pact"})Assert.IsNotNull(magic.QuerySelector("[data-testid='magic-slot-"+id+"']"));
        StringAssert.Contains(magic.QuerySelector("[data-testid='magic-slot-pact']")!.TextContent,"0 / 1");
        var conditions=cut.Find("[data-testid='conditions-panel']");var defenses=cut.Find("[data-testid='defenses-panel']");
        Assert.AreEqual("Conditions",conditions.QuerySelector("h2")!.TextContent);Assert.AreEqual("Defenses",defenses.QuerySelector("h2")!.TextContent);
        Assert.IsNull(conditions.QuerySelector("[data-testid='defenses-panel']"));
        Assert.IsTrue(magic.QuerySelector("[data-testid='magic-slot-pact'] button[aria-label='Use one Pact slots']")!.HasAttribute("disabled"));
        var panels=cut.FindComponent<CharacterStatusPanels>();
        await panels.InvokeAsync(()=>panels.Find("[data-testid='conditions-panel'] button").Click());
        Assert.IsTrue(context.JSInterop.Invocations.Any(i=>i.Identifier=="heroholdUi.openDialog"&&Equals(i.Arguments[0],"effects-dialog")));
        await panels.InvokeAsync(()=>panels.Find("[data-testid='defenses-panel'] button").Click());
        Assert.IsTrue(context.JSInterop.Invocations.Any(i=>i.Identifier=="heroholdUi.openDialog"&&Equals(i.Arguments[0],"defenses-dialog")));
        await cut.Find("[data-testid='enter-combat']").ClickAsync(new());
        await cut.Find("[data-testid='confirm-enter-combat']").ClickAsync(new());
        foreach(var id in session.Character!.Play.PreparedSpellIds)Assert.IsNotNull(cut.Find("[data-combat-section='spells']").QuerySelector("[data-combat-cast='"+id+"']"),id);
        Assert.AreEqual(1,cut.FindAll("[data-testid='conditions-panel']").Count);Assert.AreEqual(1,cut.FindAll("[data-testid='defenses-panel']").Count);
    }
    [TestMethod]
    public async Task MagicSlotCountersStaySynchronizedAfterManualUseCastingAndReload()
    {
        var session=await MagicSession();using var context=Context(session);var cut=context.Render<Home>();
        await cut.InvokeAsync(()=>cut.Find("[data-testid='magic-slot-slot1'] button[aria-label='Use one Level 1 slots']").Click());
        cut.WaitForAssertion(()=>Assert.AreEqual(1,session.Character!.Resources.Single(r=>r.Id=="slot1").Remaining));
        StringAssert.Contains(cut.Find("[data-testid='magic-slot-slot1']").TextContent,"1 / 3");Assert.AreEqual(0,cut.FindAll("[data-testid='resource-slot1']").Count,"Play shows slots once, in Magic.");
        var magic=cut.FindComponent<MagicPanel>();
        await magic.InvokeAsync(()=>magic.Find("[data-testid='cast-cure-wounds']").Click());
        Assert.AreEqual(1,session.Character!.Resources.Single(r=>r.Id=="slot1").Remaining);
        await cut.InvokeAsync(()=>cut.Find("[data-testid='confirm-cast']").Click());cut.WaitForAssertion(()=>Assert.AreEqual(0,session.Character!.Resources.Single(r=>r.Id=="slot1").Remaining));
        StringAssert.Contains(cut.Find("[data-testid='magic-slot-slot1']").TextContent,"0 / 3");
        await cut.Find("[data-testid='enter-combat']").ClickAsync(new());await cut.Find("[data-testid='confirm-enter-combat']").ClickAsync(new());
        cut.WaitForAssertion(()=>Assert.AreEqual("0",cut.Find("[data-testid='resource-slot1']").TextContent,"Combat keeps slot counters in the rail."));
        var columns=cut.FindAll(".combat-sections--split>.combat-column");Assert.AreEqual(2,columns.Count,"Spells get their own column in combat.");
        Assert.IsNotNull(columns[1].QuerySelector("[data-combat-section='spells']"));Assert.IsNull(columns[0].QuerySelector("[data-combat-section='spells']"));
        var layout=session.Character!.Combat.Layout.Sections;
        foreach(var slot in cut.FindAll(".combat-slot")){var name=slot.QuerySelector("[data-combat-section]")!.GetAttribute("data-combat-section")!;StringAssert.Contains(slot.GetAttribute("style"),"order:"+layout.IndexOf(name),"Phones keep the configured section order.");}
        await session.ReloadAsync();Assert.AreEqual(0,session.Character!.Resources.Single(r=>r.Id=="slot1").Remaining);
    }
}
