using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class JournalPanelTests
{
    private sealed class Fixture:IDisposable
    {
        private readonly string folder=Path.Combine(Path.GetTempPath(),"herohold-journal-components-"+Guid.NewGuid().ToString("N"));
        public WorkspaceSession Session {get;}
        public Fixture()=>Session=new(TestStores.Open(folder));
        public async Task Load()
        {
            await Session.InitializeAsync();var id=Session.Character!.Id;
            await Session.AddNoteAsync("Find the scout","Meet at the BRIDGE",JournalCategory.Quests,id);
            await Session.AddNoteAsync("Rowan","A friendly guide",JournalCategory.Friends,id);
            await Session.AddNoteAsync("<img src=x onerror=bad()>","<script>bad()</script>",JournalCategory.Notes,id);
        }
        public void Dispose(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestCategory("RulesData")][TestMethod] public async Task CategoriesHaveCountsAndSearchFiltersTitleOrBodyWithoutInjectingMarkup()
    {
        using var fixture=new Fixture();await fixture.Load();using var context=new BunitContext();JournalCategory? added=null;
        var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,fixture.Session).Add(c=>c.OnAdd,value=>added=value));
        Assert.AreEqual(6,cut.FindAll("[data-journal-category]").Count);Assert.AreEqual(0,cut.FindAll("img,script").Count);
        cut.Find("[data-journal-category=Quests]").Click();Assert.AreEqual(1,cut.FindAll("[data-note-id]").Count);StringAssert.Contains(cut.Markup,"Find the scout");
        cut.Find("[data-testid=journal-add]").Click();Assert.AreEqual(JournalCategory.Quests,added);
        cut.Find("[data-testid=journal-search]").Input("bridge");Assert.AreEqual(1,cut.FindAll("[data-note-id]").Count);
        cut.Find("[data-testid=journal-search]").Input("not present");Assert.AreEqual(0,cut.FindAll("[data-note-id]").Count);StringAssert.Contains(cut.Markup,"No matching entries");
        cut.Find("[data-testid=clear-journal-search]").Click();Assert.AreEqual(1,cut.FindAll("[data-note-id]").Count);
    }
    [TestCategory("RulesData")][TestMethod] public async Task ExistingEntriesCanBeEditedAndMovedWithoutChangingTheirIdentityOrCreationDate()
    {
        using var fixture=new Fixture();await fixture.Load();using var context=new BunitContext();var note=fixture.Session.Character!.Notes.Single(n=>n.Title=="Rowan");
        var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,fixture.Session));
        cut.Find($"[data-testid=edit-note-{note.Id}]").Click();cut.Find("[data-testid=journal-edit-title]").Input("Captain Rowan");cut.Find("[data-testid=journal-edit-text]").Input("Now guards the bridge");cut.Find("[data-testid=journal-edit-category]").Change("Locations");
        await cut.Find("[data-testid=journal-save-edit]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);Assert.AreEqual(note.CreatedAt,fixture.Session.Character.Notes.Single(n=>n.Id==note.Id).CreatedAt);Assert.AreEqual("Captain Rowan",fixture.Session.Character.Notes.Single(n=>n.Id==note.Id).Title);
        cut.Render();cut.Find($"[data-testid=edit-note-{note.Id}]").Click();cut.Find("[data-testid=journal-edit-category]").Change("Foes");await cut.Find("[data-testid=journal-save-edit]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);await fixture.Session.ReloadAsync();Assert.AreEqual(JournalCategory.Foes,JournalRules.Entries(fixture.Session.Character!).Single(e=>e.Note.Id==note.Id).Category);
    }
    [TestCategory("RulesData")][TestMethod] public async Task QuickNoteStartsInRequestedCategoryAndSavesCategorizedText()
    {
        using var fixture=new Fixture();await fixture.Load();using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;
        var cut=context.Render<WorkspaceDialogs>(p=>p.Add(c=>c.Character,fixture.Session.Character!).Add(c=>c.Session,fixture.Session));
        await cut.InvokeAsync(()=>cut.Instance.OpenNoteAsync(JournalCategory.Locations));Assert.AreEqual("Locations",cut.Find("#note-category").GetAttribute("value"));cut.Find("#note-title").Input("North bridge");cut.Find("#note-text").Input("Cross before sunset");await cut.Find("[data-testid=save-note]").ClickAsync(new());
        Assert.IsNull(fixture.Session.Error);Assert.AreEqual(JournalCategory.Locations,JournalRules.Entries(fixture.Session.Character!).Single(e=>e.Note.Title=="North bridge").Category);
    }
    [TestCategory("RulesData")][TestMethod] public async Task CustomCategoryCreationFiltersMovesAndEditsExistingEntries()
    {
        using var fixture=new Fixture();await fixture.Load();using var context=new BunitContext();string? addCategory=null;
        var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,fixture.Session).Add(c=>c.OnAddCategory,value=>addCategory=value));
        cut.Find("[data-testid=journal-new-category]").Input("Our caravan");await cut.Find("[data-testid=journal-create-category]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);
        cut.Find("[data-journal-category='Our caravan']").Click();StringAssert.Contains(cut.Markup,"No entries in Our caravan");cut.Find("[data-testid=journal-add]").Click();Assert.AreEqual("Our caravan",addCategory);
        cut.Find("[data-testid=journal-all]").Click();var note=fixture.Session.Character!.Notes.Single(n=>n.Title=="Rowan");
        cut.Find($"[data-testid=edit-note-{note.Id}]").Click();cut.Find("[data-testid=journal-edit-category]").Change("Our caravan");await cut.Find("[data-testid=journal-save-edit]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);
        cut.Find("[data-journal-category='Our caravan']").Click();Assert.AreEqual(1,cut.FindAll("[data-note-id]").Count);
        cut.Find($"[data-testid=edit-note-{note.Id}]").Click();cut.Find("[data-testid=journal-edit-title]").Input("Caravan captain");cut.Find("[data-testid=journal-edit-category]").Change("Quests");await cut.Find("[data-testid=journal-save-edit]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);Assert.AreEqual(0,cut.FindAll("[data-note-id]").Count);
        cut.Find("[data-testid=journal-all]").Click();cut.Find("[data-testid=journal-search]").Input("caravan captain");Assert.AreEqual(1,cut.FindAll("[data-note-id]").Count);
        cut.Find("[data-testid=journal-new-category]").Input("OUR CARAVAN");await cut.Find("[data-testid=journal-create-category]").ClickAsync(new());Assert.IsNotNull(fixture.Session.Error);StringAssert.Contains(cut.Markup,"already exists");
    }
    [TestCategory("RulesData")][TestMethod] public async Task QuickNoteOffersCustomCategoriesAndKeepsTheirAssignment()
    {
        using var fixture=new Fixture();await fixture.Load();await fixture.Session.CreateJournalCategoryAsync("Treasure",fixture.Session.Character!.Id);using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;
        var cut=context.Render<WorkspaceDialogs>(p=>p.Add(c=>c.Character,fixture.Session.Character!).Add(c=>c.Session,fixture.Session));
        await cut.InvokeAsync(()=>cut.Instance.OpenNoteAsync("Treasure"));Assert.AreEqual("Treasure",cut.Find("#note-category").GetAttribute("value"));Assert.IsTrue(cut.FindAll("#note-category option").Any(o=>o.TextContent=="Treasure"));
        cut.Find("#note-category").Change("Treasure");cut.Find("#note-title").Input("Silver key");cut.Find("#note-text").Input("Found by the bridge");await cut.Find("[data-testid=save-note]").ClickAsync(new());Assert.IsNull(fixture.Session.Error);Assert.AreEqual("Treasure",JournalRules.NamedEntries(fixture.Session.Character!).Single(e=>e.Note.Title=="Silver key").Category);
    }
    [TestCategory("RulesData")][TestMethod][DataRow(false)][DataRow(true)]
    public async Task SwitchingCharactersClearsCustomFilterAndUnsavedJournalUi(bool parentRerenders)
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-journal-switch-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=TestStores.Open(directory);var first=JournalRules.CreateCategory(SampleWorkspace.Create().Character with{Id="journal-first"},"Treasure");
            first=JournalRules.Add(first,"First key","A clue","Treasure");var second=SampleWorkspace.Create().Character with{Id="journal-second"};await repository.SaveAsync(new(){Character=first},0);await repository.SaveAsync(new(){Character=second},0);
            var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(first.Id);using var context=new BunitContext();string? added=null;
            var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,session).Add(c=>c.OnAddCategory,value=>added=value));
            cut.Find("[data-journal-category=Treasure]").Click();cut.Find("[data-testid=journal-search]").Input("First key");cut.Find("[data-testid=journal-new-category]").Input("Unsaved category");var note=first.Notes.Single(n=>n.Title=="First key");cut.Find($"[data-testid=edit-note-{note.Id}]").Click();
            await cut.InvokeAsync(()=>session.SwitchCharacterAsync(second.Id));if(parentRerenders)cut.Render();
            cut.WaitForAssertion(()=>Assert.AreEqual("",cut.Find("[data-testid=journal-search]").GetAttribute("value")));
            Assert.AreEqual("",cut.Find("[data-testid=journal-new-category]").GetAttribute("value"));Assert.AreEqual("true",cut.Find("[data-testid=journal-all]").GetAttribute("aria-pressed"));Assert.AreEqual(0,cut.FindAll("[data-testid=journal-edit-title]").Count);
            await cut.Find("[data-testid=journal-add]").ClickAsync(new());Assert.AreEqual("Notes",added);Assert.IsFalse(JournalRules.CategoryNames(session.Character!).Contains("Treasure"));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ArchivingKeepsOriginalTextSearchableAndRestoresItsCategoryAfterReload()
    {
        using var fixture=new Fixture();await fixture.Load();using var context=new BunitContext();
        var note=fixture.Session.Character!.Notes.Single(n=>n.Title=="Rowan");
        var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,fixture.Session));
        cut.Find($"[data-testid=edit-note-{note.Id}]").Click();
        cut.Find("[data-testid=journal-edit-text]").Input("Unsaved changes");
        Assert.IsTrue(cut.Find("[data-testid=journal-archive-entry]").HasAttribute("disabled"),"Archiving must not silently discard an edit.");
        cut.Find("[data-testid=journal-edit-text]").Input(note.Text);
        await cut.Find("[data-testid=journal-archive-entry]").ClickAsync(new());
        Assert.IsNull(fixture.Session.Error);Assert.AreEqual(0,cut.FindAll($"[data-note-id='{note.Id}']").Count);
        await fixture.Session.ReloadAsync();cut.Render();cut.Find("[data-testid=journal-archive]").Click();
        Assert.AreEqual(1,cut.FindAll($"[data-note-id='{note.Id}']").Count);
        cut.Find("[data-testid=journal-search]").Input("friendly guide");
        Assert.AreEqual(note.Text,fixture.Session.Character!.Notes.Single(n=>n.Id==note.Id).Text);
        await cut.Find($"[data-testid=restore-note-{note.Id}]").ClickAsync(new());
        cut.Find("[data-journal-category=Friends]").Click();
        Assert.AreEqual(1,cut.FindAll($"[data-note-id='{note.Id}']").Count);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task LongEntriesShowOneReadableExcerptOrFullTextWithoutDuplicatingTheBody()
    {
        using var fixture=new Fixture();await fixture.Load();var text="A long campaign account. "+new string('x',1800)+" Final clue.";
        await fixture.Session.AddNoteAsync("The old tower",text,JournalCategory.Lore,fixture.Session.Character!.Id);
        var note=fixture.Session.Character!.Notes.Single(n=>n.Title=="The old tower");using var context=new BunitContext();
        var cut=context.Render<JournalPanel>(p=>p.Add(c=>c.Session,fixture.Session));
        var entry=cut.Find($"[data-note-id='{note.Id}']");Assert.AreEqual(1,entry.QuerySelectorAll(".journal-entry-text").Length);
        Assert.IsTrue(entry.QuerySelector(".journal-entry-text")!.TextContent.Length<300);
        cut.Find($"[data-testid=expand-note-{note.Id}]").Click();entry=cut.Find($"[data-note-id='{note.Id}']");
        Assert.AreEqual(1,entry.QuerySelectorAll(".journal-entry-text").Length);Assert.AreEqual(text,entry.QuerySelector(".journal-entry-text")!.TextContent);
    }
}
