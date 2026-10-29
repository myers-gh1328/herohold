using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;
using Aspose.Pdf;
namespace Herohold.Documents.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class PdfCodecTests
{
    [TestMethod]
    public void ExistingEditableTemplatePreservesEveryFieldIncludingTrailingSpaces()
    {
        var result=PdfCodec.Inspect(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"CharacterSheet.pdf")));
        Assert.AreEqual(3,result.PageCount);
        Assert.AreEqual(334,result.Fields.Count);
        Assert.IsTrue(result.Fields.ContainsKey("Race "));
        Assert.IsTrue(result.Fields.ContainsKey("DEXmod "));
        Assert.IsFalse(result.Fields["CHARACTER IMAGE"]!.GetValue<bool>());
        Assert.AreEqual("exact",result.TemplateMatch);
        Assert.AreEqual("review_required",result.Status);
        Assert.IsNull(result.EmbeddedCanonical);
    }
    [TestMethod]
    public void EditableExportRoundTripsUnicodeMultilineCheckboxAndCanonicalAttachment()
    {
        var character=new JsonObject{["schema_version"]=5,["name"]="Éowyn — Жрица Ω",["level"]=1};
        var fields=new JsonObject{["CharacterName"]="Éowyn — Жрица Ω",["Features and Traits"]="Hunter’s Mark\nSecond line: café Ω",["Check Box 12"]=true,["Race "]="Human"};
        var result=PdfCodec.Inspect(PdfCodec.FillTemplate(fields,PdfEnvelope.Create(character,fields)));
        Assert.AreEqual("ready",result.Status);
        Assert.AreEqual("embedded_state",result.SourceKind);
        Assert.AreEqual(334,result.Fields.Count);
        Assert.AreEqual(character["name"]!.GetValue<string>(),result.Fields["CharacterName"]!.GetValue<string>());
        Assert.AreEqual("Hunter’s Mark\nSecond line: café Ω",result.Fields["Features and Traits"]!.GetValue<string>());
        Assert.IsTrue(result.Fields["Check Box 12"]!.GetValue<bool>());
        Assert.AreEqual("Éowyn — Жрица Ω",result.EmbeddedCanonical!["name"]!.GetValue<string>());
    }
    [TestMethod]
    public void AttachmentFreeFormProvidesReconciliationEvidenceAndOnlySafePassthrough()
    {
        var character=new JsonObject{["schema_version"]=5,["name"]="Fictional Astra",["level"]=3};
        var fields=new JsonObject{["CharacterName"]="Fictional Astra",["ClassLevel"]="Fighter 3",["Race "]="Human",["Background"]="Soldier",["STR"]="17",["HPMax"]="34",["PlayerName"]="Fictional Player",["Check Box 12"]=true,["Spells 1023"]="Cure Wounds"};
        using var document=Document.Open(PdfCodec.FillTemplate(fields,PdfEnvelope.Create(character,fields)));
        document.EmbeddedFiles!.Delete();var result=PdfCodec.Inspect(document.ToArray());
        Assert.AreEqual("review_required",result.Status);
        Assert.AreEqual("Fighter",result.ExtractedData["identity"]!["class_name"]!.GetValue<string>());
        Assert.AreEqual(3,result.ExtractedData["identity"]!["level"]!.GetValue<int>());
        Assert.AreEqual(17,result.ExtractedData["abilities"]!["Strength"]!.GetValue<int>());
        Assert.AreEqual(34,result.ExtractedData["hit_points"]!["maximum"]!.GetValue<int>());
        Assert.AreEqual("Cure Wounds",result.ExtractedData["spells"]!["by_level"]!["1"]![0]!.GetValue<string>());
        Assert.AreEqual("Fictional Player",result.PassthroughFields["PlayerName"]!.GetValue<string>());
        Assert.AreEqual(2,result.PassthroughFields.Count);
        Assert.IsTrue(result.PassthroughFields["Check Box 12"]!.GetValue<bool>());
        Assert.IsFalse(result.PassthroughFields.ContainsKey("HPMax"));
        Assert.IsFalse(result.PassthroughFields.ContainsKey("CHARACTER IMAGE"));
    }
    [TestMethod]
    public void DuplicateBuilderAttachmentsAreRejectedBeforeAdoptingEitherPayload()
    {
        var payload=PdfEnvelope.Create(new(){["schema_version"]=5,["level"]=1,["name"]="Fictional duplicate"},new());
        var result=PdfCodec.Inspect(SyntheticPdf.DuplicateBuilderAttachments(payload));
        Assert.AreEqual("error",result.Status);
        Assert.AreEqual("payload_attachment_ambiguous",result.Issues.Single().Code);
    }
    [TestMethod]
    public void ExternalVisibleFieldChangesRequireReviewAndKeepEmbeddedCanonicalUnchanged()
    {
        var character=new JsonObject{["schema_version"]=5,["level"]=1,["name"]="Fictional original"};
        var fields=new JsonObject{["CharacterName"]="Fictional original"};
        using var document=Document.Open(PdfCodec.FillTemplate(fields,PdfEnvelope.Create(character,fields)));
        document.Form!.Fields.Single(f=>f.FullName=="CharacterName").Value="Fictional external edit";
        var result=PdfCodec.Inspect(document.ToArray());
        Assert.AreEqual("review_required",result.Status);
        Assert.AreEqual("Fictional original",result.EmbeddedCanonical!["name"]!.GetValue<string>());
        Assert.AreEqual("Fictional external edit",result.PayloadInfo["changed_fields"]!["CharacterName"]!["observed"]!.GetValue<string>());
        Assert.IsTrue(result.Issues.Any(i=>i.Code=="builder_owned_fields_changed"));
    }
    [TestMethod]
    public void PasswordProtectedPdfGetsAnExplicitEncryptedError()
    {
        using var document=Document.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"CharacterSheet.pdf")));
        document.Encrypt("fictional-password","fictional-owner",Permissions.PrintDocument,CryptoAlgorithm.AESx256);
        var result=PdfCodec.Inspect(document.ToArray());
        Assert.AreEqual("error",result.Status);
        Assert.AreEqual("pdf_encrypted",result.Issues.Single().Code,result.Issues.Single().Label);
    }
    [TestMethod]
    public void OversizedCoreValueIsIgnoredWithoutLosingTheRecognizedTemplate()
    {
        using var document=Document.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"CharacterSheet.pdf")));
        document.Form!.Fields.Single(f=>f.FullName=="CharacterName").Value=new string('x',100_001);
        var result=PdfCodec.Inspect(document.ToArray());
        Assert.AreEqual("exact",result.TemplateMatch);
        Assert.AreEqual("review_required",result.Status);
        Assert.IsFalse(result.Fields.ContainsKey("CharacterName"));
        Assert.IsTrue(result.Issues.Any(i=>i.Code=="form_field_value_too_long"));
    }
}
