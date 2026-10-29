using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativeReferenceUiTests
{
    [TestMethod]
    public async Task InitialSampleDialogShowsActualClassSourcesAndDescriptionsWithoutAnExportOrSave()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-reference-ui-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=SampleWorkspace.Create().Character with{Id="initial-reference-ui",Features=[..SampleWorkspace.Create().Character.Features,"Homebrew ribbon"]};var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo,TestStores.Workflow);await session.SwitchCharacterAsync(c.Id);var before=System.Text.Json.JsonSerializer.Serialize(await repo.LoadAsync(c.Id));
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<OwnedReferencesDialog>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));await cut.InvokeAsync(()=>cut.Instance.OpenAsync());
            var classes=cut.Find("[data-testid='features-class']");StringAssert.Contains(classes.TextContent,"Favored Enemy");StringAssert.Contains(classes.TextContent,"Ranger");Assert.IsFalse(classes.TextContent.Contains("class:Ranger"));Assert.IsFalse(cut.Markup.Contains("Build / imported source"));Assert.IsTrue(classes.QuerySelectorAll("details p").Any(p=>p.TextContent.Length>50));StringAssert.Contains(cut.Find("[data-testid='features-other']").TextContent,"Homebrew ribbon");Assert.IsFalse(cut.Find("[data-testid='features-other']").TextContent.Contains("Favored Enemy"));Assert.AreEqual(1L,session.Snapshot!.Revision);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(await repo.LoadAsync(c.Id)));Assert.IsEmpty(session.Character!.Derived.FeatureDetails,"Reading references never writes them into the sheet.");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
