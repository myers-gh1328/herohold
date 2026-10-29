using Herohold.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Documents;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativePdfImportReviewTests
{
    private sealed class InspectionService(PdfInspection inspection) : IPdfService
    {
        public Task<PdfInspection> InspectAsync(byte[] bytes, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(inspection); }
        public Task<byte[]> ExportAsync(Herohold.Core.CharacterState character, JsonObject canonical, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private static NativePdfImportReviewService Service(PdfInspection inspection) => new(NativeCharacterBuilderTests.Builder(), NativeCatalogIntegrationTests.Snapshot(), new InspectionService(inspection));
    /// <summary>The recovered character as the rules calculate it, in the sheet's document shape.</summary>
    private static JsonObject Sheet(PdfReview review) => BuilderDocuments.Document(NativeCharacterBuilderTests.Builder().Calculate(review.Draft, null, false));
    private static JsonObject Selections(PdfReview review) => JsonSerializer.SerializeToNode(review.Review.Selections)!.AsObject();
    private static ChoiceValue Typed(JsonObject selections) => JsonSerializer.Deserialize<ChoiceValue>(selections.ToJsonString())!;
    private static string Text(object value) => JsonSerializer.Serialize(value);

    [TestMethod]
    public async Task EmbeddedCharacterIsDetachedAndValidatedWithoutChangingVisibleEditedFieldsIntoBuildState()
    {
        var original=NativeCharacterBuilderTests.Fighter();var before=original.ToJsonString();
        var inspection=new PdfInspection{SourceKind="embedded_state",Status="review_required",EmbeddedCanonical=original,Issues=[new("builder_owned_fields_changed","warning","comparison","The visible name changed.")],Fields=new(){["CharacterName"]="Different visible name"}};
        var result=await Service(inspection).ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsTrue((result.Review.CanAdopt==true),Text(result.Review));
        Assert.AreEqual("Native Test Fighter",Sheet(result)["name"]!.GetValue<string>());
        Assert.AreEqual("builder_owned_fields_changed",result.Review.Issues[0].Code);
        Assert.AreEqual(before,original.ToJsonString());
        Assert.IsTrue(result.Choices.Count>0);
    }

    [TestMethod]
    public async Task EmbeddedIncompleteCharacterCannotBeAdoptedUntilRealSourceChoicesAreCompleted()
    {
        var original=NativeCharacterBuilderTests.Fighter();original["level"]=3;
        var result=await Service(new(){SourceKind="embedded_state",Status="ready",EmbeddedCanonical=original}).ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsFalse((result.Review.CanAdopt==true));
        Assert.IsTrue(result.Review.Issues.Count>0);
        Assert.IsTrue(result.Choices.Any(d=>d.Id=="subclass"));
        Assert.IsTrue(result.Review.SourceCanAdopt);
        var rules=NativeWorkflowTests.Workflow();
        var opened=new DraftResult{Draft=result.Draft,Validation=result.Validation,Review=result.Review};
        var repaired=rules.Revalidate(rules.Edit(opened.Draft,["subclass"],ChoiceValue.Of("Champion")),result.Review);
        Assert.IsTrue(repaired.Review!.CanAdopt==true,Text(repaired.Review));
        Assert.IsFalse(rules.Revalidate(repaired,result.Review with{SourceCanAdopt=false}).Review!.CanAdopt==true,"A source the review refused stays refused after edits.");
    }

    private static PdfInspection Raw() => new()
    {
        SourceKind="compatible_form",Status="review_required",TemplateMatch="exact",
        PassthroughFields=new(){["PlayerName"]="Fictional player"},
        ExtractedData=new(){["identity"]=new JsonObject{["name"]="Fictional imported fighter",["class_name"]="Fighter",["level"]=1,["species"]="Orc",["background"]="Soldier"},
            ["abilities"]=new JsonObject{["Strength"]=17,["Dexterity"]=14,["Constitution"]=14,["Intelligence"]=10,["Wisdom"]=12,["Charisma"]=8},
            ["hit_points"]=new JsonObject{["maximum"]=19,["current"]=7,["temporary"]=3},["equipment"]=new JsonObject{["raw"]="Longsword; Mysterious keepsake"},
            ["spells"]=new JsonObject{["by_level"]=new JsonObject()},["comparison"]=new JsonObject{["armor_class"]=99},["presentation"]=new JsonObject()}
    };
    private static JsonObject ConfirmedSelections(JsonObject initial)
    {
        var s=initial.DeepClone().AsObject();s["ability_scores_confirmed"]=true;s["equipment_confirmed"]=true;s["spells_confirmed"]=true;s["feats_confirmed"]=true;s["masteries_confirmed"]=true;
        s["origin_languages"]=new JsonArray("Elvish","Dwarvish");
        s["background_primary_ability"]="Strength";s["background_secondary_ability"]="Dexterity";s["background_options"]=new JsonObject{["tool_proficiencies"]=new JsonArray("Dice Set")};
        s["class_skills"]=new JsonArray("Perception","Survival");s["class_options"]=new JsonObject{["fighting_style"]="Defense"};s["weapon_masteries"]=new JsonArray("Longsword","Shortbow","Spear");return s;
    }
    [TestMethod]
    public async Task RawEvidenceRemainsBlockedUntilConfirmedAndReconstructsFinalAbilitiesHpAndCustomEquipment()
    {
        var service=Service(Raw());var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsFalse((initial.Review.CanAdopt==true));
        var selections=Selections(initial);
        Assert.AreEqual("Fighter",selections["class_name"]!.GetValue<string>());
        Assert.IsFalse(selections["ability_scores_confirmed"]!.GetValue<bool>());
        Assert.IsTrue(selections["custom_equipment"]!.AsArray().Any(n=>n?.GetValue<string>()=="Mysterious keepsake"));
        Assert.IsTrue(initial.Choices.Any(d=>d.Id=="import_final_scores_confirmed"&&d.Path[0]=="import_selections"));
        var confirmed=await service.ReviewAsync([1],Typed(ConfirmedSelections(selections)));
        Assert.IsTrue((confirmed.Review.CanAdopt==true),Text(confirmed.Review));
        var c=Sheet(confirmed);Assert.AreEqual(15,c["base_ability_scores"]!["Strength"]!.GetValue<int>());Assert.AreEqual(13,c["base_ability_scores"]!["Dexterity"]!.GetValue<int>());
        Assert.AreEqual(17,c["ability_scores"]!["Strength"]!.GetValue<int>());Assert.AreEqual(19,c["hit_points"]!["maximum"]!.GetValue<int>());Assert.AreEqual(7,c["hit_points"]!["current"]!.GetValue<int>());Assert.AreEqual(3,c["hit_points"]!["temporary"]!.GetValue<int>());
        Assert.AreEqual("pdf_import_checkpoint",c["level_history"]![0]!["source"]!.GetValue<string>());
        Assert.AreEqual("Fictional player",c["pdf_passthrough_fields"]!["PlayerName"]!.GetValue<string>());
        Assert.IsTrue(confirmed.Review.Comparisons.Any(n=>n.Label=="Armor Class"&&n.Status=="conflict"));
    }
    [TestMethod]
    public async Task UnmatchedSpellsAndMissingFinalScoresRemainBlockersEvenAfterConfirmation()
    {
        var raw=Raw();raw.ExtractedData["spells"]!["by_level"]!["1"]=new JsonArray("Unknown fictional spell");raw.ExtractedData["abilities"]!["Charisma"]=null;
        var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));var confirmed=await service.ReviewAsync([1],Typed(ConfirmedSelections(Selections(initial))));
        Assert.IsFalse((confirmed.Review.CanAdopt==true));
        var codes=confirmed.Review.Issues.Select(n=>n.Code).ToArray();CollectionAssert.Contains(codes,"unmatched_spells");CollectionAssert.Contains(codes,"ability_charisma_missing");
    }
    [TestMethod]
    public async Task CappedFinalScoreUsesExplainedBonusBaselineInsteadOfUnobservableExtraPoints()
    {
        var raw=Raw();raw.ExtractedData["abilities"]!["Strength"]=20;
        var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));var result=await service.ReviewAsync([1],Typed(ConfirmedSelections(Selections(initial))));
        Assert.AreEqual(18,Sheet(result)["base_ability_scores"]!["Strength"]!.GetValue<int>());
        Assert.AreEqual(20,Sheet(result)["ability_scores"]!["Strength"]!.GetValue<int>());
    }
    [TestMethod]
    public async Task ActualCompiledWorkerExportsEditablePdfAndReviewsEmbeddedRawExternalEditsAndDamagedUploads()
    {
        var builder=NativeCharacterBuilderTests.Builder();var native=builder.ResultFor(NativeCharacterBuilderTests.Fighter());
        var worker=new PdfWorkerClient(Path.Combine(AppContext.BaseDirectory,"pdf-worker","Herohold.PdfWorker.dll"));
        var service=new NativePdfImportReviewService(builder,NativeCatalogIntegrationTests.Snapshot(),worker);
        var canonical=native["character"]!.AsObject();canonical["name"]="Fictional native PDF Ω";
        var bytes=await worker.ExportAsync(Herohold.TestSupport.TestStores.Character(native),canonical);
        var embedded=await service.ReviewAsync(bytes,ChoiceValue.Record([]));Assert.IsTrue((embedded.Review.CanAdopt==true),Text(embedded.Review));
        Assert.AreEqual("Fictional native PDF Ω",Sheet(embedded)["name"]!.GetValue<string>());
        using var edited=Aspose.Pdf.Document.Open(bytes);edited.Form!.Fields.Single(f=>f.FullName=="STR").Value="30";
        var changed=await service.ReviewAsync(edited.ToArray(),ChoiceValue.Record([]));Assert.AreEqual(12,Sheet(changed)["ability_scores"]!["Strength"]!.GetValue<int>());
        Assert.IsTrue(changed.Review.Issues.Any(n=>n.Code=="builder_owned_fields_changed"));
        edited.EmbeddedFiles!.Delete();var raw=await service.ReviewAsync(edited.ToArray(),ChoiceValue.Record([]));Assert.IsFalse((raw.Review.CanAdopt==true));
        Assert.AreEqual(30,Selections(raw)["ability_scores"]!["Strength"]!.GetValue<int>());
        await Assert.ThrowsExactlyAsync<PdfFormatException>(()=>service.ReviewAsync("not a PDF"u8.ToArray(),ChoiceValue.Record([])));
        using var unrelated=new Aspose.Pdf.Document();unrelated.Pages.Add();await Assert.ThrowsExactlyAsync<PdfFormatException>(()=>service.ReviewAsync(unrelated.ToArray(),ChoiceValue.Record([])));
    }
    [TestMethod]
    public async Task RecoveredFeatNeedsItsOwnChoicesAndKeepsAnExplicitSourceForItsAbilityIncrease()
    {
        var raw=Raw();raw.ExtractedData["identity"]!["level"]=4;raw.ExtractedData["presentation"]!["features"]="Skill Expert";
        var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));
        var s=ConfirmedSelections(Selections(initial));s["subclass"]="Champion";s["weapon_masteries"]!.AsArray().Add("Greatsword");
        var blocked=await service.ReviewAsync([1],Typed(s));Assert.IsFalse((blocked.Review.CanAdopt==true));
        var featId=s["general_feat_ids"]![0]!.GetValue<string>();
        Assert.IsTrue(blocked.Choices.Any(n=>n.Path.Count==4&&n.Path[1]=="feat_choices"&&n.Path[3]=="skill_proficiency"));
        s["feat_choices"]=new JsonObject{[featId]=new JsonObject{["ability"]="Dexterity",["skill_proficiency"]="Insight",["expertise"]="Perception"}};
        var result=await service.ReviewAsync([1],Typed(s));Assert.IsTrue((result.Review.CanAdopt==true),Text(result.Review));
        Assert.AreEqual(12,Sheet(result)["base_ability_scores"]!["Dexterity"]!.GetValue<int>());
        Assert.AreEqual(14,Sheet(result)["ability_scores"]!["Dexterity"]!.GetValue<int>());
        Assert.IsTrue(Sheet(result)["ability_score_adjustments"]!.AsArray().Any(n=>n?["source_name"]?.GetValue<string>()=="Skill Expert"&&n["adjustments"]?["Dexterity"]?.GetValue<int>()==1));
        CollectionAssert.Contains(Sheet(result)["expertise"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray(),"Perception");
    }
    [TestMethod]
    public async Task RecoveredBardSpellsUse2024PreparedSelectionAndCanBeReviewedInTheActualPicker()
    {
        var raw=Raw();raw.ExtractedData["identity"]!["class_name"]="Bard";raw.ExtractedData["spells"]!["by_level"]!["1"]=new JsonArray("Cure Wounds");
        var result=await Service(raw).ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsTrue(Selections(result)["spells"]!["prepared"]!.AsArray().Any(n=>n?.GetValue<string>()=="Cure Wounds"));
        var picker=result.Choices.Single(d=>d.Id=="import_spells_prepared");
        CollectionAssert.Contains(ChoiceReading.Names(picker.Value).ToArray(),"Cure Wounds");
    }
    [TestMethod]
    public async Task SecondaryOriginSkilledVirtualChoicesBecomeIndependentNativeSources()
    {
        var raw=Raw();raw.ExtractedData["identity"]!["species"]="Human";raw.ExtractedData["identity"]!["background"]="Artisan";
        var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));var s=ConfirmedSelections(Selections(initial));
        s["background_secondary_ability"]="Constitution";
        s["species_options"]=new JsonObject{["origin_feats"]=new JsonArray("Skilled"),["skill_proficiencies"]=new JsonArray("Stealth")};
        s["origin_feat_choices"]=new JsonObject{["tools"]=new JsonArray("Carpenter's Tools","Mason's Tools","Smith's Tools")};
        s["folio_origin_feat_choices"]=new JsonObject{["Skilled"]=new JsonObject{["proficiencies"]=new JsonArray("Skill: Insight","Skill: Medicine","Tool: Disguise Kit")}};
        var result=await service.ReviewAsync([1],Typed(s));var skilled=Sheet(result)["folio_origin_feat_choices"]!["Skilled"]!;
        Assert.IsNull(skilled["proficiencies"]);CollectionAssert.AreEquivalent(new[]{"Insight","Medicine"},skilled["skills"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        CollectionAssert.Contains(Sheet(result)["skill_proficiencies"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray(),"Insight");
    }
    [TestMethod]
    public async Task DuplicateRecoveredFeatSelectionIsRejectedAsStructuredReviewError()
    {
        var raw=Raw();var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));var s=ConfirmedSelections(Selections(initial));
        s["general_feat_ids"]=new JsonArray("skill_expert_xphb","skill_expert_xphb");
        var error=await Assert.ThrowsExactlyAsync<PdfFormatException>(()=>service.ReviewAsync([1],Typed(s)));Assert.AreEqual("invalid_review",error.Code);
    }
    [TestMethod]
    public async Task RawPdfExplicitOriginLanguagesAndThreeBackgroundIncreasesUseTheNativeBuilder()
    {
        var raw=Raw();raw.ExtractedData["presentation"]!["proficiencies_and_languages"]="Armor: Light armor\nLanguages: Common, Infernal,\n Elvish";
        var service=Service(raw);var first=await service.ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsTrue(first.Choices.Any(n=>n.Id=="import_origin_languages"));
        var s=ConfirmedSelections(Selections(first));s["background_options"]!["ability_bonus_mode"]="three";
        var result=await service.ReviewAsync([1],Typed(s));Assert.IsTrue((result.Review.CanAdopt==true),Text(result.Review));
        CollectionAssert.AreEquivalent(new[]{"Elvish","Dwarvish"},Sheet(result)["origin_languages"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        CollectionAssert.Contains(Sheet(result)["languages"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray(),"Infernal");
        Assert.AreEqual(16,Sheet(result)["base_ability_scores"]!["Strength"]!.GetValue<int>());Assert.AreEqual(13,Sheet(result)["base_ability_scores"]!["Constitution"]!.GetValue<int>());
        Assert.IsFalse(result.Choices.Any(n=>n.Id=="import_primary_ability"));
    }
    [TestMethod]
    public async Task HistoricalResilientRemainsImportableAfterMonkFourteenGrantsEverySave()
    {
        var c=NativeCharacterBuilderTests.Fighter();c["name"]="Fictional historical Monk";c["class"]="Monk";c["level"]=14;c["subclass"]="Warrior of Shadow";c["weapon_masteries"]=new JsonArray();c["class_options"]=new JsonObject{["class_skills"]=new JsonArray("History","Insight"),["tool_proficiencies"]=new JsonArray("Flute")};
        var source=new JsonObject{["type"]="class_level",["class_name"]="Monk",["level"]=4};
        c["general_feats"]=new JsonArray(new JsonObject{["instance_id"]="historic-resilient",["feat_id"]="resilient_xphb",["name"]="Resilient",["source"]=source.DeepClone(),["choices"]=new JsonObject{["ability"]="Wisdom"}});
        c["ability_score_adjustments"]=new JsonArray(new JsonObject{["source_type"]="feat",["source_id"]="feat:historic-resilient",["feat_instance_id"]="historic-resilient",["source_name"]="Resilient",["source"]=source.DeepClone(),["maximum"]=20,["adjustments"]=new JsonObject{["Wisdom"]=1}});
        var native=NativeCharacterBuilderTests.Builder().ResultFor(c);Assert.IsTrue(native["validation"]!["complete"]!.GetValue<bool>(),native["validation"]!.ToJsonString());
        var result=await Service(new(){SourceKind="embedded_state",Status="ready",EmbeddedCanonical=c}).ReviewAsync([1],ChoiceValue.Record([]));
        Assert.IsTrue((result.Review.CanAdopt==true),Text(result.Review.Issues));
    }
    [TestMethod]
    public async Task RecoveredSheetKeepsOnlyItsOwnEquipmentAndObservedHitPoints()
    {
        var raw=Raw();var service=Service(raw);var initial=await service.ReviewAsync([1],ChoiceValue.Record([]));
        var result=await service.ReviewAsync([1],Typed(ConfirmedSelections(Selections(initial))));
        Assert.IsFalse(result.Draft.IncludeDefaultEquipment,"The sheet lists the character's equipment; class starting gear is not added.");
        CollectionAssert.AreEqual(new[]{"Longsword","Mysterious keepsake"},Sheet(result)["equipment"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray());
        Assert.AreEqual(12,Sheet(result)["armor_class"]!.GetValue<int>(),"Armor Class comes from the recovered gear: no armor, Dexterity 14.");
        Assert.AreEqual((7,3),(result.Draft.Facts!.CurrentHp,result.Draft.Facts.TemporaryHp));
        var s=ConfirmedSelections(Selections(initial));s["observed_current_hp"]=-1;s["observed_temporary_hp"]=200000;
        var invalid=await service.ReviewAsync([1],Typed(s));
        CollectionAssert.IsSubsetOf(new[]{"observed_current_hp_invalid","observed_temporary_hp_invalid"},invalid.Review.Issues.Select(i=>i.Code).ToArray());
        Assert.IsFalse(invalid.Review.CanAdopt==true);
        Assert.IsTrue(invalid.Draft.Facts!.CurrentHp is >= 0 and <= 19&&invalid.Draft.Facts.TemporaryHp!=200000,"Out-of-range sheet HP is not taken.");
    }
}
