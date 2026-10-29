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
public sealed class PlayerPresentationTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task SaveAndLibraryShowPlayerInformationWithoutStorageRevisionNumbers()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-presentation-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session).AddSingleton(new HeroholdFeatures(Combat:true));
            var sheet=context.Render<Home>();Assert.AreEqual("Saved",sheet.Find("[data-testid=save-status]").TextContent);
            var library=context.Render<CharacterLibrary>(p=>p.Add(c=>c.Session,session));
            Assert.IsFalse(library.Find(".library-grid").TextContent.Contains("Revision",StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1L,session.Snapshot!.Revision,"Keep internal concurrency protection.");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public void EmptyDefensesUseBriefPlayerLanguage()
    {
        using var context=new BunitContext();var cut=context.Render<DefenseSummary>();
        Assert.AreEqual("None listed",cut.Find(".context-defense-summary").TextContent);
    }
    [TestMethod]
    public void RecentJournalKeepsShortReadablePreviewsWithoutChangingFullNotes()
    {
        using var context=new BunitContext();var text="First line\n\n"+new string('x',9000)+" PRIVATE END";
        var note=new JournalNote{Id="long-note",Title="A campaign lead",Text=text};
        var cut=context.Render<NoteList>(p=>p.Add(c=>c.Notes,new[]{note}));
        Assert.IsTrue(cut.Find(".context-note-item p").TextContent.Length<=181);
        Assert.IsFalse(cut.Markup.Contains("PRIVATE END"));Assert.AreEqual(text,note.Text);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AnUnsetTargetIsOneOptionalControlRatherThanAnEmptyStatusCard()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-target-presentation-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();using var context=new BunitContext();
            context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session).AddSingleton(new HeroholdFeatures(Combat:true));var cut=context.Render<Home>();
            Assert.AreEqual(0,cut.FindAll(".known-target-tool").Count);
            Assert.AreEqual("Target defenses",cut.Find("[data-testid=target-defenses]").TextContent);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task CombatEntryLivesWithSheetToolsAndStillRequiresTheExistingConfirmation()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-combat-entry-layout-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();using var context=new BunitContext();
            context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session).AddSingleton(new HeroholdFeatures(Combat:true));var cut=context.Render<Home>();
            Assert.IsNotNull(cut.Find("nav.sheet-tool-nav [data-testid=enter-combat]"));
            await cut.Find("[data-testid=enter-combat]").ClickAsync(new());
            Assert.IsNotNull(cut.Find("[data-testid=confirm-enter-combat]"));
            Assert.IsFalse(session.Character!.Combat.Enabled);Assert.AreEqual(1L,session.Snapshot!.Revision);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public void LongItemDescriptionsShowPreviewAndKeepFullText()
    {
        using var context=new BunitContext();
        var text="Ranged Weapon · Gear · Ammunition (Range 150/600) · Heavy · Two-Handed. You can use a weapon that has the Ammunition property to make a ranged attack only if you have ammunition to fire from it.";
        var item=context.Render<ItemDescription>(p=>p.Add(c=>c.Text,text));
        var preview=item.Find(".item-description-preview").TextContent;
        Assert.IsTrue(preview.Length<=ItemDescription.PreviewLength+1&&preview.EndsWith('…'),preview);
        Assert.AreEqual(text,item.Find("details.item-description p").TextContent,"The full description stays available.");
        var shortItem=context.Render<ItemDescription>(p=>p.Add(c=>c.Text,"Gear · +1 fire damage."));
        Assert.AreEqual(0,shortItem.FindAll("details").Count);Assert.AreEqual("Gear · +1 fire damage.",shortItem.Find("p").TextContent);
    }
    [TestMethod]
    public void CombatSpellLockReasonIsShownOnceWhenEverySpellSharesIt()
    {
        const string reason="Record whose creature turn is active before taking this combat action.";
        Assert.AreEqual(reason,CombatWorkspace.SharedLockReason([reason,reason,reason]));
        Assert.AreEqual("",CombatWorkspace.SharedLockReason([reason,"No level 3 slots remain."]),"Different reasons stay on their rows.");
        Assert.AreEqual("",CombatWorkspace.SharedLockReason([reason]),"A single spell keeps its own reason.");
        Assert.AreEqual("",CombatWorkspace.SharedLockReason(["",""]));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task CombatModeStaysHiddenUnlessTheFeatureIsOn()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-presentation-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);
            var cut=context.Render<Home>();
            Assert.AreEqual(0,cut.FindAll("[data-testid=enter-combat]").Count);Assert.AreEqual(0,cut.FindAll(".combat-workspace").Count);
            await cut.Find("[data-testid=nav-equipment]").ClickAsync(new());Assert.IsNotNull(cut.Find(".inventory-view [data-testid=inventory-weight]"),"Inventory replaces the middle section.");
            Assert.AreEqual(0,cut.FindAll("[data-testid=adventure-magic]").Count);
            await cut.Find("[data-testid=nav-companions]").ClickAsync(new());Assert.IsNotNull(cut.Find(".companions-view [data-testid=open-adventure-facts]"));
            await cut.Find("[data-testid=nav-adventure]").ClickAsync(new());Assert.IsNotNull(cut.Find("[data-testid=adventure-magic]"));
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public void SpellCastingTimeNamesTheTurnResource()
    {
        Assert.AreEqual("Action",SpellText.CastingTime("1 action"));Assert.AreEqual("Bonus action",SpellText.CastingTime("1 bonus"));
        Assert.AreEqual("Bonus action",SpellText.CastingTime("1 bonus action"));Assert.AreEqual("Reaction, when you take damage",SpellText.CastingTime("1 reaction, when you take damage"));
        Assert.AreEqual("1 minute",SpellText.CastingTime("1 minute"));Assert.AreEqual("Bonus action",SpellText.CastingTime("Bonus action"));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task FailedCharacterChangesAreLoggedWithTheirCause()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-presentation-"+Guid.NewGuid().ToString("N"));
        try
        {
            var log=new ListLogger();var session=new WorkspaceSession(TestStores.Open(folder),logger:log);await session.InitializeAsync();
            await session.SwitchCharacterAsync("no-such-character");
            Assert.IsNotNull(session.Error,"Players still see the short message.");
            var entry=log.Entries.Single();Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Warning,entry.Level);Assert.IsNotNull(entry.Error);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    private sealed class ListLogger:Microsoft.Extensions.Logging.ILogger<WorkspaceSession>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level,Exception? Error,string Message)> Entries{get;}=[];
        public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level)=>true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level,Microsoft.Extensions.Logging.EventId id,TState state,Exception? error,Func<TState,Exception?,string> format)=>Entries.Add((level,error,format(state,error)));
    }
}
