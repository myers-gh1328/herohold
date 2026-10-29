using Herohold.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class WorkflowSessionTests
{
 private string directory="";
 [TestInitialize] public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-workflow-"+Guid.NewGuid().ToString("N"));
 [TestCleanup] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
 private sealed class FakeBridge():FakeRules(_=>new JsonObject{["validation"]=new JsonObject{["complete"]=true,["missing"]=new JsonArray()},["choices"]=new JsonArray()});
 private static FakeRules Incomplete()=>new(_=>new JsonObject{["validation"]=new JsonObject{["complete"]=false,["missing"]=new JsonArray()},["choices"]=new JsonArray()});
 [TestMethod] public async Task DraftEditsAndCancellationLeaveActiveCharacterAndRevisionUntouched()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();
  var before=session.Snapshot;await session.StartEditAsync();
  await session.EditDraftAsync(["name"],ChoiceValue.Of("Draft name"));
  Assert.AreEqual("Draft name",session.Workflow!.Candidate.Name);
  Assert.AreEqual(before,session.Snapshot);session.CancelWorkflow();
  Assert.IsNull(session.Workflow);Assert.AreEqual(before,session.Snapshot);
 }
 [TestMethod] public async Task SameNamedCreateUsesNewIdentityAndLibraryCanSwitchWithoutOverwriting()
 {
  var repo=TestStores.Open(directory);var session=new WorkspaceSession(repo,new FakeBridge());await session.InitializeAsync();
  var oldId=session.Character!.Id;await session.StartCreateAsync();await session.CommitWorkflowAsync();
  Assert.IsNull(session.Error);Assert.AreNotEqual(oldId,session.Character!.Id);Assert.AreEqual(2,session.Library.Count);
  await session.SwitchCharacterAsync(oldId);Assert.AreEqual(oldId,session.Character!.Id);
  Assert.IsNotNull(await repo.LoadAsync(oldId));
 }
 [TestMethod] public async Task StaleBuildDraftCannotEraseAResourceChange()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();
  await session.StartEditAsync();await session.EditDraftAsync(["name"],ChoiceValue.Of("Old draft"));
  await session.AdjustResourceAsync("luck",-1);await session.CommitWorkflowAsync();
  Assert.IsNotNull(session.Error);Assert.AreEqual(2,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
  Assert.AreNotEqual("Old draft",session.Character.Name);
 }
 [TestMethod] public async Task IncompleteDraftCannotBeCommitted()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),Incomplete());await session.InitializeAsync();
  var before=session.Snapshot;await session.StartCreateAsync();await session.CommitWorkflowAsync();
  Assert.IsNotNull(session.Error);Assert.AreEqual(before,session.Snapshot);Assert.AreEqual(1,session.Library.Count);
 }
 [TestMethod] public async Task NativeWorkspaceExportReviewRequiresExplicitAdoptionAndPreservesOriginal()
 {
  var repo=TestStores.Open(directory);var session=new WorkspaceSession(repo,new FakeBridge());await session.InitializeAsync();
  var before=session.Snapshot;var artifact=await session.ExportAsync("workspace");Assert.IsNotNull(artifact);
  await session.ReviewUploadAsync(artifact.FileName,artifact.Bytes);Assert.AreEqual(before,session.Snapshot);
  Assert.AreEqual("import",session.Workflow!.Kind);
  await session.AdoptImportAsync();Assert.IsNull(session.Error);Assert.AreNotEqual(before!.Character.Id,session.Character!.Id);
  Assert.AreEqual(2,session.Library.Count);Assert.IsNotNull(await repo.LoadAsync(before.Character.Id));
 }
 private sealed class DelayedBridge():FakeRules(_=>new JsonObject{["validation"]=new JsonObject{["complete"]=true},["choices"]=new JsonArray()})
 {
  public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public TaskCompletionSource Result=new(TaskCreationOptions.RunContinuationsAsynchronously);
  protected override void Step(string operation){Started.TrySetResult();Result.Task.Wait();}
 }
 [TestMethod] public async Task CancelledPendingDraftCannotReappearAfterCompatibilityReply()
 {
  var bridge=new DelayedBridge();var session=new WorkspaceSession(TestStores.Open(directory),bridge);await session.InitializeAsync();
  var before=session.Snapshot;var pending=session.StartCreateAsync();await bridge.Started.Task;
  session.CancelWorkflow();bridge.Result.SetResult();
  await pending;Assert.IsNull(session.Workflow);Assert.AreEqual(before,session.Snapshot);Assert.IsFalse(session.IsBusy);
 }

 [TestMethod] public async Task UnsupportedNativeWorkspaceSchemaCannotEnterImportReview()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();
  var exported=await session.ExportAsync("workspace");var document=JsonNode.Parse(exported!.Bytes)!.AsObject();document["schemaVersion"]=999;
  var before=session.Snapshot;await session.ReviewUploadAsync("future.herohold.json",System.Text.Encoding.UTF8.GetBytes(document.ToJsonString()));
  Assert.IsNotNull(session.Error);Assert.IsNull(session.Workflow);Assert.AreEqual(before,session.Snapshot);
 }

 [TestMethod] public async Task BrandNewCharacterStartsAtFullHpWhileExistingBuildEditsDoNotHeal()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();
  Assert.AreEqual(32,session.Character!.CurrentHp);await session.StartEditAsync();await session.CommitWorkflowAsync();
  Assert.AreEqual(32,session.Character!.CurrentHp);
  await session.StartCreateAsync();await session.CommitWorkflowAsync();
  Assert.IsNull(session.Error);Assert.AreEqual(session.Character!.MaximumHp,session.Character.CurrentHp);
 }

 private sealed class PdfReviewBridge():FakeRules(_=>throw new InvalidOperationException("PDF review must be routed back through its evidence."))
 {
  public override Task<DraftResult> ImportPdfAsync(byte[] pdf,ChoiceValue? selections=null,CancellationToken ct=default)
  {
   var choices=new JsonArray();foreach(var key in new[]{"confirmed","other"})choices.Add(new JsonObject{["id"]=key,["type"]="boolean",["path"]=new JsonArray("import_selections",key),["value"]=selections?.At([key]) is {IsNull:false} chosen?JsonSerializer.SerializeToNode(chosen):JsonValue.Create(false)});
   return Task.FromResult(new FakeRules(new JsonObject{["validation"]=new JsonObject{["complete"]=true},["choices"]=choices,["importReview"]=new JsonObject{["selections"]=JsonSerializer.SerializeToNode(selections??ChoiceValue.Record([])),["canAdopt"]=false}}).ImportJson(new()));
  }
 }
 [TestMethod] public async Task PdfReviewChoicesKeepEarlierConfirmationsAndRevalidateOriginalEvidence()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new PdfReviewBridge());await session.InitializeAsync();
  await session.ReviewUploadAsync("review.pdf","%PDF-fictional"u8.ToArray());
  bool Selected(string id)=>session.Workflow!.Choices.Single(c=>c.Id==id).Value.Flag is true;
  await session.ChooseWorkflowAsync("confirmed",ChoiceValue.Of(true));
  Assert.IsNull(session.Error);Assert.IsTrue(Selected("confirmed"));
  await session.ChooseWorkflowAsync("other",ChoiceValue.Of(true));
  Assert.IsNull(session.Error);Assert.IsTrue(Selected("confirmed"),"An earlier confirmation is kept when the review is rerun.");
  Assert.IsTrue(Selected("other"));
 }

 [TestMethod] public async Task AcceptedCharacterSaveRemainsSavedWhenAnotherLibraryEntryIsCorrupt()
 {
  var repo=TestStores.Open(directory);var session=new WorkspaceSession(repo,new FakeBridge());await session.InitializeAsync();
  await File.WriteAllTextAsync(Path.Combine(directory,"unrelated.json"),"{broken");
  await session.StartCreateAsync();await session.CommitWorkflowAsync();
  Assert.IsNull(session.Error);StringAssert.StartsWith(session.SaveStatus,"Saved");
  Assert.IsNull(session.Workflow);Assert.AreNotEqual(SampleWorkspace.CharacterId,session.Character!.Id);
  Assert.IsNotNull(await repo.LoadAsync(session.Character.Id));
 }

 [TestMethod] public async Task RenderedOldCharacterCounterCannotSpendNewCharacterAtSameRevision()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();var oldId=session.Character!.Id;
  await session.StartCreateAsync();await session.CommitWorkflowAsync();Assert.AreEqual(1L,session.Snapshot!.Revision);
  var before=session.Snapshot;await session.AdjustResourceAsync("luck",-1,oldId);
  Assert.IsNotNull(session.Error);Assert.AreEqual(before,session.Snapshot);
  Assert.AreEqual(3,session.Character!.Resources.Single(r=>r.Id=="luck").Remaining);
 }

 [TestMethod] public async Task OversizedNativeWorkspaceNeverPromisesItCanBeAdopted()
 {
  var session=new WorkspaceSession(TestStores.Open(directory),new FakeBridge());await session.InitializeAsync();
  var original=SampleWorkspace.Create().Character;
  var c=original with{Build=original.Build! with{MigrationWarnings=[new string('x',900000)]},Play=original.Play with{Spells=Enumerable.Range(0,400).Select(i=>new SpellDefinition{Id="manual-"+i,Name="Manual spell "+i,Description=new string('x',3000)}).ToList()}};
  var options=new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
  var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new WorkspaceSnapshot{Character=c},options);
  var before=session.Snapshot;await session.ReviewUploadAsync("too-large.herohold.json",bytes);
  Assert.IsNotNull(session.Error);Assert.IsNull(session.Workflow);Assert.AreEqual(before,session.Snapshot);
 }

}
