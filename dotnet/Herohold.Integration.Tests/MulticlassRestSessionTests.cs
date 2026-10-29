using Herohold.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class MulticlassRestSessionTests
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
    [TestMethod]public async Task MetadataProjectionRestReloadAndCanonicalExportPreserveSourceChoicesAndPoolSpending()
    {
        var basis=SampleWorkspace.Create().Character;var calculation=TestStores.Calculation(basis);
        calculation=calculation with{Build=calculation.Build with{ClassTracks=[new(){Id="class-wizard",ClassName="Wizard",Level=1,Choices=new(){["spells"]=ChoiceValue.Record([new("prepared",ChoiceValue.ListOf([ChoiceValue.Of("Shield")]))])}}]}};
        var sources=new List<SpellSourceState>{new(){Id="class-wizard",ClassTrackId="class-wizard",Name="Wizard",Ability=Ability.Intelligence,PreparedSpellIds=["wizard-shield"],SpellbookIds=["wizard-shield","wizard-detect"],Preparation=new(){MaximumSpellLevel=1,CanChangeOnLongRest=true,FromSpellbook=true,PreparedLimit=2,EligibleSpellIds=["wizard-shield","wizard-detect"]}}};
        sources.Add(new(){Id="class_feature:Wizard gift",ClassTrackId="class-wizard",SourceKind="class_feature",Name="Wizard gift",Ability=Ability.Intelligence,Preparation=new(){MaximumSpellLevel=1},GrantedSpellIds=["gift-spell"]});
        CharacterCalculation Multiclass(CharacterCalculation c)=>c with{SpellSources=sources,OwnedSpells=[new(){Id="wizard-shield",Name="Shield",Level=1},new(){Id="wizard-detect",Name="Detect Magic",Level=1},new(){Id="gift-spell",Name="Gift",Level=1}],HitDiePools=[new(){Id="hd6",Sides=6,Maximum=1,Remaining=1,ClassTrackIds=["class-wizard"]}]};
        var projected=WorkspaceProjection.FromCalculation(Multiclass(calculation));Assert.AreEqual(2,projected.Play.SpellSources.Count);Assert.AreEqual("wizard-shield",projected.Play.SpellSources[0].PreparedSpellIds[0]);Assert.AreEqual(1,projected.Play.HitDiePools.Count);
        var directory=Path.Combine(Path.GetTempPath(),"herohold-multiclass-rest-"+Guid.NewGuid().ToString("N"));
        try{
            var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=projected with{CurrentHp=1}},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(projected.Id);
            await session.RestAsync(new(){Kind=RestKind.Long,SourceCharacterId=projected.Id,SourceRevision=session.Snapshot!.Revision,SourceChanges=[new(){SpellSourceId="class-wizard",ChangeSpells=true,NewPreparedSpellIds=["wizard-detect"]}]});Assert.IsNull(session.Error,session.Error);
            await session.ReloadAsync();Assert.AreEqual("wizard-detect",session.Character!.Play.SpellSources[0].PreparedSpellIds[0]);var exported=WorkspaceProjection.ToCanonical(session.Character);Assert.AreEqual("Detect Magic",exported["folio_class_tracks"]![0]!["choices"]!["spells"]!["prepared"]![0]!.GetValue<string>());
            await session.RestAsync(new(){Kind=RestKind.Short,Mode=DiceMode.Physical,SourceCharacterId=projected.Id,SourceRevision=session.Snapshot!.Revision,HitDieSpends=[new(){PoolId="hd6",Count=1,PhysicalDice=[4]}]});Assert.IsNull(session.Error,session.Error);await session.ReloadAsync();Assert.AreEqual(0,session.Character!.Play.HitDiePools[0].Remaining);
            var recalculated=WorkspaceProjection.FromCalculation(Multiclass(TestStores.Calculation(session.Character with{Build=basis.Build})) with{Build=calculation.Build},session.Character);Assert.AreEqual(0,recalculated.Play.HitDiePools[0].Remaining);
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
