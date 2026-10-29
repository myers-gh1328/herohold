using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Documents.Tests;

[TestClass]
public sealed class PdfEnvelopeTests
{
    [TestMethod]
    public void PortableEnvelopeMatchesExistingPythonCanonicalChecksumWithUnicodeAndFloats()
    {
        var character=JsonNode.Parse("""{"schema_version":5,"level":1,"name":"Éowyn Ω","values":[1.0,0.000001,1e16,1.25,"line\u2028break","🐉"]}""")!.AsObject();
        var snapshot=new JsonObject{["CharacterName"]="Éowyn Ω"};
        var bytes=PdfEnvelope.Create(character,snapshot);
        var envelope=PdfEnvelope.Parse(bytes);
        Assert.AreEqual("0e73dde90d56d2cd573e7f1db62dc4ec4006d49ec262092043b88621da8af462",envelope["character_sha256"]!.GetValue<string>());
        Assert.AreEqual("dnd-character-builder.character",envelope["format_id"]!.GetValue<string>());
        Assert.AreEqual("Éowyn Ω",envelope["character"]!["name"]!.GetValue<string>());
        Assert.AreEqual("Éowyn Ω",envelope["owned_pdf_field_snapshot"]!["CharacterName"]!.GetValue<string>());
        Assert.IsTrue(Encoding.UTF8.GetString(bytes).Contains("🐉"));
    }
    [TestMethod]
    [DataRow("format_id","42","payload_unknown_format")]
    [DataRow("payload_version","\"1\"","payload_unsupported_version")]
    [DataRow("character_schema_version","true","payload_invalid_schema_version")]
    [DataRow("character_schema_version","6","payload_unsupported_schema")]
    public void MalformedEnvelopeTypesHaveStableReviewErrors(string key,string value,string expected)
    {
        var envelope=JsonNode.Parse(PdfEnvelope.Create(new(){["schema_version"]=5,["name"]="Fictional",["level"]=1},new()))!.AsObject();
        envelope[key]=JsonNode.Parse(value);
        var error=Assert.Throws<PdfFormatException>(()=>PdfEnvelope.Parse(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
        Assert.AreEqual(expected,error.Code);
    }
    [TestMethod]
    [DataRow("class","false")]
    [DataRow("ability_scores","[]")]
    public void EmbeddedCharacterBasicsAreValidatedBeforeReturningCanonicalState(string key,string value)
    {
        var character=new JsonObject{["schema_version"]=5,["level"]=1,["name"]="Fictional"};character[key]=JsonNode.Parse(value);
        var payload=PdfEnvelope.Create(character,new());
        var error=Assert.Throws<PdfFormatException>(()=>PdfEnvelope.Parse(payload));
        Assert.AreEqual("payload_invalid_character",error.Code);
    }
}
