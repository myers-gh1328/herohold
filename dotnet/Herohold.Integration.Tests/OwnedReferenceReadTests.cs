using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class OwnedReferenceReadTests
{
    [TestMethod]
    public async Task InitialSampleReferencesUseActualOwnedSourcesWithoutExportingOrSavingTheCharacter()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-owned-read-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=SampleWorkspace.Create().Character with{Id="owned-reference-read",Features=[..SampleWorkspace.Create().Character.Features,"Homebrew ribbon"]};var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo,NativeWorkflowTests.Workflow());await session.SwitchCharacterAsync(c.Id);var before=System.Text.Json.JsonSerializer.Serialize(await repo.LoadAsync(c.Id));var built=System.Text.Json.JsonSerializer.Serialize(session.Character!.Build);var revision=session.Snapshot!.Revision;
            var references=await session.ReadOwnedFeatureReferencesAsync(c.Id);Assert.IsNotNull(references);var favored=references.Single(r=>r.Name=="Favored Enemy");Assert.AreEqual("class:Ranger",favored.Source);Assert.IsGreaterThan(50,favored.Description.Length);
            Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(built,System.Text.Json.JsonSerializer.Serialize(session.Character.Build));Assert.IsEmpty(session.Character.Derived.FeatureDetails);Assert.IsTrue(session.Character.Features.Contains("Homebrew ribbon"));Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(await repo.LoadAsync(c.Id)));Assert.IsNull(session.Error);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
