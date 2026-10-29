using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestClass]
public sealed class SheetSessionTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-sheet-session",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [TestCategory("RulesData")][TestMethod]public async Task ProfilePurseAndInspirationPersistAndStaleCharacterEditorCannotWrite()
    {
        var session=new WorkspaceSession(TestStores.Open(directory));await session.InitializeAsync();var id=session.Character!.Id;
        await session.UpdateProfileAsync(new(){Appearance="A silver scar",Languages=["Common","Elvish"],Alignment="Neutral Good",Bonds="Keep the village safe"},12345,id);Assert.IsNull(session.Error);
        await session.ChangeCoinsAsync(CoinChangeKind.Receive,new(){Gp=25,Sp=3,Ep=1},id);Assert.IsNull(session.Error);await session.SetHeroicInspirationAsync(true,id);
        var reload=new WorkspaceSession(TestStores.Open(directory));await reload.InitializeAsync();Assert.AreEqual(25,SheetRules.Purse(reload.Character!).Gp);Assert.AreEqual(12345,SheetRules.ExperiencePoints(reload.Character!));Assert.AreEqual("A silver scar",SheetRules.Profile(reload.Character!).Appearance);Assert.IsTrue(SheetRules.HasHeroicInspiration(reload.Character!));
        var revision=reload.Snapshot!.Revision;await reload.ChangeCoinsAsync(CoinChangeKind.Spend,new(){Gp=1,Sp=9},id);Assert.IsNotNull(reload.Error);Assert.AreEqual(revision,reload.Snapshot.Revision);Assert.AreEqual(25,SheetRules.Purse(reload.Character!).Gp);
        await reload.UpdateProfileAsync(new(){Appearance="Wrong"},9,"another-character");Assert.IsNotNull(reload.Error);Assert.AreEqual(revision,reload.Snapshot.Revision);Assert.AreEqual("A silver scar",SheetRules.Profile(reload.Character!).Appearance);
    }
    [TestMethod]public void PdfExportUsesRealXpInsteadOfZero()
    {
        var c=SheetRules.UpdateProfile(SampleWorkspace.Create().Character,new(){Alignment="Neutral"},7777);
        var fields=Herohold.Documents.PdfFieldMapper.Create(c);Assert.AreEqual("7777",fields["XP"]!.GetValue<string>());
    }
    [TestCategory("RulesData")][TestMethod]public void CanonicalAndPdfExportsRoundTripActualSheetValuesWithoutOverwritingBuildSources()
    {
        var c=SampleWorkspace.Create().Character;var profile=new CharacterProfile{Background="Manual history",Size="Medium",Appearance="Scarred",Backstory="A northern road",Alignment="Neutral",Languages=["Common"],Ideals="Mercy"};
        c=SheetRules.UpdateProfile(c,profile,7777);c=SheetRules.ChangeCoins(c,CoinChangeKind.Set,new(){Cp=1,Sp=2,Ep=3,Gp=4,Pp=5});c=SheetRules.SetHeroicInspiration(c,true);
        var canonical=WorkspaceProjection.ToCanonical(c);var restored=TestStores.Restored(canonical);Assert.AreEqual(7777,SheetRules.ExperiencePoints(restored));Assert.AreEqual(5,SheetRules.Purse(restored).Pp);Assert.AreEqual("Scarred",SheetRules.Profile(restored).Appearance);Assert.IsTrue(SheetRules.HasHeroicInspiration(restored));
        var fields=Herohold.Documents.PdfFieldMapper.Create(c);Assert.AreEqual("7777",fields["XP"]!.GetValue<string>());Assert.AreEqual("4",fields["GP"]!.GetValue<string>());Assert.AreEqual("Neutral",fields["Alignment"]!.GetValue<string>());Assert.AreEqual("A northern road",fields["Backstory"]!.GetValue<string>());
    }
    [TestCategory("RulesData")][TestMethod]public void ExhaustionIsNotBakedIntoStandingAdjustmentsOnRepeatedProjection()
    {
        var c=SampleWorkspace.Create().Character with{Exhaustion=2};var baseline=c with{Exhaustion=0};
        var stats=NativeExportProjection.Create(c);Assert.AreEqual(CharacterCalculations.Save(baseline,Ability.Wisdom).Total,stats["saving_throw_bonuses"]!["Wisdom"]!.GetValue<int>());
        var first=TestStores.Workflow.Recalculate(CharacterDraft.Of(c),c);var second=TestStores.Workflow.Recalculate(CharacterDraft.Of(first),first);
        Assert.AreEqual(2,first.Exhaustion);Assert.AreEqual(CharacterCalculations.Save(first,Ability.Wisdom).Total,CharacterCalculations.Save(second,Ability.Wisdom).Total);Assert.AreEqual(first.Speed,second.Speed);
        Assert.AreEqual(CharacterCalculations.Save(first with{Exhaustion=0},Ability.Wisdom).Total-4,CharacterCalculations.Save(first,Ability.Wisdom).Total,"Two levels of Exhaustion take 4 from a save, once.");
    }
    [TestMethod]public void ExportDoesNotReplaceUnresolvedLegacyXpAndCoinsWithZero()
    {
        var c=SampleWorkspace.Create().Character with{Build=new(){PdfPassthroughFields=new(){["GP"]=new(){Text="12.5"},["XP"]=new(){Text="unreadable"}}}};var canonical=WorkspaceProjection.ToCanonical(c);Assert.IsNull(canonical["coins"]);Assert.IsNull(canonical["experience_points"]);var fields=Herohold.Documents.PdfFieldMapper.Create(c);Assert.IsFalse(fields.ContainsKey("GP"));Assert.IsFalse(fields.ContainsKey("XP"));Assert.AreEqual("12.5",canonical["pdf_passthrough_fields"]!["GP"]!.GetValue<string>());
    }
    [TestCategory("RulesData")][TestMethod]public async Task AnotherConfirmedSheetActionClosesTheImmediateInspirationWindow()
    {
        var session=new WorkspaceSession(TestStores.Open(directory));await session.InitializeAsync();var id=session.Character!.Id;await session.SetHeroicInspirationAsync(true,id);
        await session.RollAsync(new(){OperationId="immediate-roll",SourceCharacterId=id,SourceRevision=session.Snapshot!.Revision,Name="Perception",Kind=RollKind.Skill,Ability=Ability.Wisdom,Skill="Perception",Mode=DiceMode.Physical,PhysicalD20=[15]});
        await session.ChangeCoinsAsync(CoinChangeKind.Receive,new(){Gp=1},id);var revision=session.Snapshot!.Revision;
        await session.UseHeroicInspirationAsync(new(){OperationId="too-late",SourceCharacterId=id,SourceRevision=revision,RollId="immediate-roll",DieIndex=0,PhysicalDie=1});Assert.IsNotNull(session.Error);Assert.AreEqual(revision,session.Snapshot.Revision);Assert.IsTrue(SheetRules.HasHeroicInspiration(session.Character!));
    }
    [TestCategory("RulesData")][TestMethod]public async Task LoadingACharacterNeverSavesIt()
    {
        var repo=TestStores.Open(directory);var saved=await repo.SaveAsync(SampleWorkspace.Create(),0);
        async Task<string> Stamp(){await using var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(directory,"herohold.db")+";Pooling=False");await connection.OpenAsync();await using var sql=connection.CreateCommand();sql.CommandText="SELECT Revision||' '||UpdatedAt FROM characters";return (string)(await sql.ExecuteScalarAsync())!;}
        var before=await Stamp();var loaded=await TestStores.Open(directory).LoadAsync(saved.Character.Id);
        Assert.IsNotNull(loaded!.Character.Sheet);Assert.AreEqual(saved.Revision,loaded.Revision);Assert.AreEqual(before,await Stamp());
    }
}
