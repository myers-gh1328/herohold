using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")]
[TestClass]
public sealed class JournalSessionTests
{
    [TestMethod] public async Task CategorizedEditsPersistAcrossReloadAndPortableProjectionWithoutChangingTheNoteShape()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-journal-persistence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();var sourceId=session.Character!.Id;
            await session.AddNoteAsync("North bridge","Find the scout",JournalCategory.Quests,sourceId);Assert.IsNull(session.Error);var note=session.Character.Notes.Single(n=>n.Title=="North bridge");
            await session.EditJournalNoteAsync(note.Id,"The scout","A trusted friend",JournalCategory.Friends,sourceId);Assert.IsNull(session.Error);
            var reloaded=new WorkspaceSession(TestStores.Open(folder));await reloaded.InitializeAsync();Assert.AreEqual(JournalCategory.Friends,JournalRules.Entries(reloaded.Character!).Single(e=>e.Note.Id==note.Id).Category);Assert.AreEqual(note.CreatedAt,reloaded.Character!.Notes.Single(n=>n.Id==note.Id).CreatedAt);
            var canonical=CanonicalCharacterRules.Migrate(WorkspaceProjection.ToCanonical(reloaded.Character));var projected=TestStores.Restored(canonical);Assert.AreEqual(JournalCategory.Friends,JournalRules.Entries(projected).Single(e=>e.Note.Id==note.Id).Category);Assert.AreEqual(3,reloaded.Snapshot!.SchemaVersion);
            await reloaded.MoveJournalNoteAsync(note.Id,JournalCategory.Notes,sourceId);Assert.IsNull(reloaded.Error);await reloaded.ReloadAsync();Assert.AreEqual(JournalCategory.Notes,JournalRules.Entries(reloaded.Character!).Single(e=>e.Note.Id==note.Id).Category);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod] public async Task AStaleCircuitOrWrongCharacterCannotOverwriteTheSavedJournal()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-journal-conflict-"+Guid.NewGuid().ToString("N"));
        try
        {
            var first=new WorkspaceSession(TestStores.Open(folder));await first.InitializeAsync();var id=first.Character!.Id;await first.AddNoteAsync("Scout","A lead",JournalCategory.Quests,id);var noteId=first.Character.Notes.Single(n=>n.Title=="Scout").Id;
            var second=new WorkspaceSession(TestStores.Open(folder));await second.InitializeAsync();await first.MoveJournalNoteAsync(noteId,JournalCategory.Friends,id);Assert.IsNull(first.Error);
            await second.EditJournalNoteAsync(noteId,"Stale edit","Must not replace",JournalCategory.Foes,id);Assert.IsNotNull(second.Error);await second.ReloadAsync();Assert.AreEqual("Scout",second.Character!.Notes.Single(n=>n.Id==noteId).Title);Assert.AreEqual(JournalCategory.Friends,JournalRules.Entries(second.Character).Single(e=>e.Note.Id==noteId).Category);
            var count=second.Character.Notes.Count;await second.AddNoteAsync("Wrong source","Must not save",JournalCategory.Notes,"another-character");Assert.IsNotNull(second.Error);Assert.AreEqual(count,second.Character.Notes.Count);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod] public async Task CustomGroupsAndAssignmentsPersistThroughReloadPortableRoundtripAndConflicts()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-custom-journal-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder));await session.InitializeAsync();var id=session.Character!.Id;
            await session.CreateJournalCategoryAsync("Our caravan",id);Assert.IsNull(session.Error);await session.CreateJournalCategoryAsync("Empty group",id);Assert.IsNull(session.Error);
            await session.AddNoteAsync("Silver key","Found at the bridge","Our caravan",id);Assert.IsNull(session.Error);var note=session.Character.Notes.Single(n=>n.Title=="Silver key");
            await session.EditJournalNoteAsync(note.Id,"Gate key","Keep safe","our CARAVAN",id);Assert.IsNull(session.Error);await session.ReloadAsync();Assert.AreEqual("Our caravan",JournalRules.NamedEntries(session.Character!).Single(e=>e.Note.Id==note.Id).Category);
            var canonical=CanonicalCharacterRules.Migrate(WorkspaceProjection.ToCanonical(session.Character!));var projected=TestStores.Restored(canonical);CollectionAssert.Contains(JournalRules.CategoryNames(projected).ToArray(),"Empty group");Assert.AreEqual("Our caravan",JournalRules.NamedEntries(projected).Single(e=>e.Note.Id==note.Id).Category);Assert.AreEqual(note.CreatedAt,projected.Notes.Single(n=>n.Id==note.Id).CreatedAt);Assert.AreEqual(3,session.Snapshot!.SchemaVersion);
            var stale=new WorkspaceSession(TestStores.Open(folder));await stale.InitializeAsync();await session.MoveJournalNoteAsync(note.Id,"Empty group",id);Assert.IsNull(session.Error);await stale.CreateJournalCategoryAsync("Stale group",id);Assert.IsNotNull(stale.Error);await stale.ReloadAsync();Assert.IsFalse(JournalRules.CategoryNames(stale.Character!).Contains("Stale group"));Assert.AreEqual("Empty group",JournalRules.NamedEntries(stale.Character!).Single(e=>e.Note.Id==note.Id).Category);
            await stale.CreateJournalCategoryAsync("Wrong source","other-character");Assert.IsNotNull(stale.Error);Assert.IsFalse(JournalRules.CategoryNames(stale.Character!).Contains("Wrong source"));
            await session.MoveJournalNoteAsync(note.Id,JournalCategory.Notes,id);Assert.IsNull(session.Error);Assert.AreEqual("Notes",JournalRules.NamedEntries(session.Character!).Single(e=>e.Note.Id==note.Id).Category);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public async Task ArchivedOriginalsSurvivePortableCopyAndRejectConcurrentOrWrongOwnerWrites()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-archive-persistence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var first=new WorkspaceSession(TestStores.Open(folder));await first.InitializeAsync();var owner=first.Character!.Id;
            await first.CreateJournalCategoryAsync("Original sheet",owner);await first.AddNoteAsync("Original upload","All original source text","Original sheet",owner);
            var note=first.Character!.Notes.Single(n=>n.Title=="Original upload");var stale=new WorkspaceSession(TestStores.Open(folder));await stale.InitializeAsync();
            await first.ArchiveJournalNoteAsync(note.Id,true,owner);Assert.IsNull(first.Error);
            await stale.ArchiveJournalNoteAsync(note.Id,true,owner);Assert.IsNotNull(stale.Error);
            var projected=TestStores.Restored(CanonicalCharacterRules.Migrate(WorkspaceProjection.ToCanonical(first.Character!)));
            var copy=WorkspaceProjection.WithNewIdentity(projected);WorkspaceRules.Validate(copy);
            Assert.IsTrue(JournalRules.ArchivedNoteIds(copy).Contains(note.Id));Assert.AreEqual(note,copy.Notes.Single(n=>n.Id==note.Id));
            Assert.AreEqual("Original sheet",JournalRules.NamedEntries(copy).Single(n=>n.Note.Id==note.Id).Category);
            await stale.ReloadAsync();await stale.ArchiveJournalNoteAsync(note.Id,false,"another-character");Assert.IsNotNull(stale.Error);
            await stale.ReloadAsync();Assert.IsTrue(JournalRules.ArchivedNoteIds(stale.Character!).Contains(note.Id));
            await stale.ArchiveJournalNoteAsync(note.Id,false,owner);Assert.IsNull(stale.Error);await stale.ReloadAsync();
            Assert.AreEqual(0,JournalRules.ArchivedNoteIds(stale.Character!).Count);Assert.AreEqual(note,stale.Character!.Notes.Single(n=>n.Id==note.Id));
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
