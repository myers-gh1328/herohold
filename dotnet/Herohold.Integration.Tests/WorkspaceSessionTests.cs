using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")]
[TestClass]
public class WorkspaceSessionTests
{
 private string directory = "";
 [TestInitialize] public void Setup() => directory = Path.Combine(Path.GetTempPath(),"herohold-session-tests",Guid.NewGuid().ToString("N"));
 [TestCleanup] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
 [TestMethod] public async Task NoteIsSavedAndRecoveredByANewSession()
 {
  var first=new WorkspaceSession(TestStores.Open(directory));
  await first.InitializeAsync();
  Assert.IsTrue(first.IsLoaded);
  await first.AddNoteAsync("At the bridge","Ask the ferryman.");
  var second=new WorkspaceSession(TestStores.Open(directory));
  await second.InitializeAsync();
  Assert.IsTrue(second.Character!.Notes.Any(n=>n.Title=="At the bridge"&&n.Text=="Ask the ferryman."));
  Assert.AreEqual(first.Snapshot!.Revision,second.Snapshot!.Revision);
 }
 [TestMethod] public async Task StaleSessionCannotOverwriteNewerNoteWithResourceSpend()
 {
  var repo=TestStores.Open(directory);
  var first=new WorkspaceSession(repo);var second=new WorkspaceSession(repo);
  await first.InitializeAsync();await second.InitializeAsync();
  await first.AddNoteAsync("Recent clue","Keep this note.");
  await second.AdjustResourceAsync("luck",-1);
  Assert.IsNotNull(second.Error);
  StringAssert.Contains(second.Error.ToLowerInvariant(),"reload");
  Assert.AreEqual(3,second.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
  await second.ReloadAsync();
  Assert.IsNull(second.Error);
  Assert.IsTrue(second.Character!.Notes.Any(n=>n.Title=="Recent clue"));
 }
 [TestMethod] public async Task InvalidMutationPreservesStateAndDiskRevision()
 {
  var session=new WorkspaceSession(TestStores.Open(directory));await session.InitializeAsync();
  var revision=session.Snapshot!.Revision;
  await session.AdjustResourceAsync("luck",-99);
  Assert.IsNotNull(session.Error);Assert.AreEqual(revision,session.Snapshot.Revision);
  Assert.AreEqual(3,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
 }
 [TestMethod] public async Task CorruptWorkspaceReloadShowsErrorAndKeepsPriorVisibleState()
 {
  var session=new WorkspaceSession(TestStores.Open(directory));
  await session.InitializeAsync();
  var before=session.Snapshot;
  static async Task<object?> Sql(string db,string command){await using var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+db+";Pooling=False");await connection.OpenAsync();await using var sql=connection.CreateCommand();sql.CommandText=command;return await sql.ExecuteScalarAsync();}
  var db=Path.Combine(directory,"herohold.db");
  await Sql(db,"UPDATE character_resources SET Recovery='Sometimes' WHERE PoolId='luck'");
  await session.ReloadAsync();
  Assert.IsNotNull(session.Error);
  Assert.AreEqual(before,session.Snapshot);
  Assert.IsFalse(session.IsBusy);
  Assert.AreEqual("Sometimes",await Sql(db,"SELECT Recovery FROM character_resources WHERE PoolId='luck'"),"A damaged character is kept as it was, not overwritten.");
 }

 [TestMethod] public async Task UnsafeUndoExplainsOverlappingChangeInsteadOfSuggestingAnUnhelpfulReload()
 {
  var repo=TestStores.Open(directory);var session=new WorkspaceSession(repo);await session.InitializeAsync();
  await session.AdjustResourceAsync("luck",-1);var saved=session.Snapshot!;
  await repo.SaveAsync(saved with{Character=WorkspaceRules.AdjustResource(saved.Character,"luck",-1)},saved.Revision);
  await session.ReloadAsync();await session.UndoAsync(new(){SourceRevision=session.Snapshot!.Revision});
  Assert.IsNotNull(session.Error);StringAssert.Contains(session.Error,"overlaps");
  Assert.AreEqual(1,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
 }

}
