using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]
public sealed class AdventureFactsUiTests
{
    [TestMethod]
    public async Task CompanionAndMovementAreStagedUntilSavedThenSurviveReload()
    {
        var dir=Path.Combine(Path.GetTempPath(),"herohold-travel-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=HandMadeStore.At(dir);var c=SampleWorkspace.Create().Character with{Build=null};await repository.SaveAsync(new(){Character=c},0);
            var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);
            var cut=context.Render<AdventureDialog>(p=>p.Add(x=>x.Session,session));await cut.InvokeAsync(cut.Instance.OpenAsync);var revision=session.Snapshot!.Revision;
            cut.Find("#travel-mode-kind").Change("Fly");cut.Find("#travel-mode-speed").Change("40");cut.Find("[data-testid='stage-movement']").Click();
            cut.Find("#companion-name").Input("Owl");cut.Find("#companion-maximum").Change("5");cut.Find("#companion-current").Change("5");cut.Find("[data-testid='stage-companion']").Click();
            Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(0,session.Character!.Sheet.Adventure.Companions.Count);
            await cut.Find("[data-testid='save-adventure']").ClickAsync(new());Assert.IsNull(session.Error);await session.ReloadAsync();
            Assert.AreEqual("Owl",session.Character!.Sheet.Adventure.Companions.Single().Name);Assert.AreEqual(40,session.Character.Sheet.Adventure.Movement.Single().Speed);
        }
        finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
