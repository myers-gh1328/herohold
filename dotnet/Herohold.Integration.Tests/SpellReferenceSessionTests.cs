using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestClass]
public sealed class SpellReferenceSessionTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task NativeWizardReferenceAndOptInCantripReplacementPersistWithoutChangingOtherSources()
    {
        var result=NativeCharacterBuilderTests.Builder().ResultFor(NativeCharacterBuilderTests.Wizard());
        Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());
        var projected=TestStores.Character(result);var original=projected with{CurrentHp=projected.MaximumHp};
        Assert.IsTrue(original.Play.Preparation.CanChangeCantripsOnLongRest);Assert.AreEqual(1,original.Play.Preparation.CantripReplacementLimit);
        var familiar=original.Play.Spells.Single(s=>s.Name=="Find Familiar"&&s.CastingSource=="Wizard");
        CollectionAssert.AreEquivalent(new[]{"V","S","M"},familiar.Components);Assert.IsTrue(familiar.ComponentsKnown);Assert.AreEqual(1000,familiar.MaterialCostCp);Assert.IsTrue(familiar.MaterialConsumed);Assert.IsTrue(familiar.Ritual);
        var fire=original.Play.Spells.Single(s=>s.Name=="Fire Bolt"&&s.CastingSource=="Wizard").Id;var frost=original.Play.Spells.Single(s=>s.Name=="Ray of Frost"&&s.CastingSource=="Wizard").Id;
        var choices=original.Play.Preparation.OwnedCantripSpellIds.Select(id=>id==fire?frost:id).ToList();var grants=original.Play.GrantedSpellIds.ToArray();
        var directory=Path.Combine(Path.GetTempPath(),"herohold-wizard-rest-source-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=TestStores.Open(directory);await repository.SaveAsync(new(){Character=original},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(original.Id);
            await session.SetSpellNoteAsync(familiar.Id,"Use an owl; ask about scouting.",original.Id);Assert.IsNull(session.Error);var revision=session.Snapshot!.Revision;
            var rest=new RestRequest{OperationId="wizard-swap",SourceCharacterId=original.Id,SourceRevision=revision,Kind=RestKind.Long,ChangeCantrips=true,NewClassCantripSpellIds=choices};
            await session.RestAsync(rest);Assert.IsNull(session.Error,session.Error);Assert.AreEqual(session.Character!.MaximumHp,session.Character.CurrentHp);Assert.IsFalse(session.Character!.Play.KnownSpellIds.Contains(fire));Assert.IsTrue(session.Character.Play.KnownSpellIds.Contains(frost));CollectionAssert.AreEquivalent(grants,session.Character.Play.GrantedSpellIds);
            var savedRevision=session.Snapshot!.Revision;await session.RestAsync(rest);Assert.AreEqual(savedRevision,session.Snapshot!.Revision);
            await session.ReloadAsync();Assert.AreEqual(session.Character!.MaximumHp,session.Character.CurrentHp);Assert.AreEqual("Use an owl; ask about scouting.",session.Character!.Play.SpellNotes[familiar.Id]);Assert.IsTrue(session.Character.Play.KnownSpellIds.Contains(frost));Assert.IsFalse(session.Character.Play.KnownSpellIds.Contains(fire));
            var exported=WorkspaceProjection.ToCanonical(session.Character);Assert.AreEqual(session.Character.MaximumHp,exported["hit_points"]!["current"]!.GetValue<int>());var recalculated=TestStores.Calculated(exported,session.Character);Assert.AreEqual(session.Character.MaximumHp,recalculated.CurrentHp);StringAssert.Contains(exported["spells"]!["cantrips"]!.ToJsonString(),"Ray of Frost");Assert.IsFalse(exported["spells"]!["cantrips"]!.ToJsonString().Contains("Fire Bolt"));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void IdenticallyNamedOlderSpellDoesNotAcquire2024NumericalAutomation()
    {
        var calculation=TestStores.Calculation(SampleWorkspace.Create().Character);
        var result=WorkspaceProjection.FromCalculation(calculation with{Spells=[WorkspaceProjection.MapSpell(new Herohold.Rules.CatalogSpell{Name="Cure Wounds",Level=1,Source="PHB",Description="Original edition reference."},calculation.Build.ClassName)],Grants=[],GrantedSpellNames=[]});
        var older=result.Play.Spells.Single(s=>s.Name=="Cure Wounds"&&s.Source=="PHB");Assert.IsFalse(older.Modeled,"A same-name2014record must not inherit2024dice or scaling.");
    }
    [TestMethod]
    public async Task SpellNotesRejectUnknownOrOtherCharacterDraftsWithoutSaving()
    {
        var c=SampleWorkspace.Create().Character with{Id="owned-spell-notes",Build=null};var directory=Path.Combine(Path.GetTempPath(),"herohold-note-ownership-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);var revision=session.Snapshot!.Revision;
            await session.SetSpellNoteAsync("not-owned","Do not save",c.Id);Assert.IsNotNull(session.Error);Assert.AreEqual(revision,session.Snapshot.Revision);
            await session.SetSpellNoteAsync(c.Play.PreparedSpellIds.First(),"Do not save","other-character");Assert.IsNotNull(session.Error);Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(0,session.Character!.Play.SpellNotes.Count);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
