using Aspose.Pdf;
using Aspose.Pdf.Text;
using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Documents.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class PdfSpellContinuationTests
{
    private static CharacterState Wizard(int count)
    {
        var names=Enumerable.Range(1,count).Select(i=>i==count-1?"Mage Armor":i==count?"Protection from Evil and Good":$"Fictional Spell {i:00} Ω").ToArray();
        return PdfFieldMapperTests.FictionalCharacter() with{Name="Fictional spellbook Ω",ClassName="Wizard",Level=4,Play=new(){SpellcastingAbility=Ability.Intelligence,HitDieSize=6,SpellbookIds=names.Select((_,i)=>"spell-"+i).ToList(),Spells=names.Select((name,i)=>new SpellDefinition{Id="spell-"+i,Name=name,Level=1}).ToList()}};
    }
    [TestMethod]
    public void OverflowAddsPrintableCompleteBookWithoutChangingEditableFieldsOrEmbeddedState()
    {
        var wizard=Wizard(14);var canonical=new JsonObject{["schema_version"]=5,["name"]=wizard.Name,["level"]=4,["class"]="Wizard"};var before=canonical.ToJsonString();
        var bytes=PdfCodec.Export(wizard,canonical);using var document=Document.Open(bytes);
        Assert.IsGreaterThan(3,document.Pages.Count,"Fourteen level1 spells must not be silently cut off by the eleven form rows.");
        var absorber=new TextAbsorber();for(var page=4;page<=document.Pages.Count;page++)absorber.Visit(document.Pages[page]);
        foreach(var spell in wizard.Play.Spells)StringAssert.Contains(absorber.Text,spell.Name);
        StringAssert.Contains(absorber.Text,"Level 1");
        var marker=new TextAbsorber();marker.Visit(document.Pages[3]);StringAssert.Contains(marker.Text,"Complete spell list continues on page 4.");
        Assert.AreEqual(612d,document.Pages[4].Rect.Width);Assert.AreEqual(792d,document.Pages[4].Rect.Height);
        Assert.AreEqual(334,document.Form!.Fields.Length);Assert.AreEqual(before,canonical.ToJsonString());
        var inspected=PdfCodec.Inspect(bytes);Assert.AreEqual("ready",inspected.Status);Assert.AreEqual("exact",inspected.TemplateMatch);Assert.AreEqual(334,inspected.Fields.Count);Assert.IsTrue(JsonNode.DeepEquals(canonical,inspected.EmbeddedCanonical));
    }
    [TestMethod]
    public void ABookWithinItsFormCapacityKeepsTheOriginalThreePageSheet()
    {
        using var document=Document.Open(PdfCodec.Export(Wizard(11),new(){["schema_version"]=5,["name"]="Fictional compact book",["level"]=4}));
        Assert.AreEqual(3,document.Pages.Count);Assert.AreEqual(334,document.Form!.Fields.Length);
    }
    [TestMethod]
    public async Task CompiledWorkerPaginatesACompleteBookAndRetainsDetachedFormEvidence()
    {
        var wizard=Wizard(60);var canonical=new JsonObject{["schema_version"]=5,["name"]=wizard.Name,["level"]=4};
        var worker=new PdfWorkerClient(Path.Combine(AppContext.BaseDirectory,"pdf-worker","Herohold.PdfWorker.dll"));
        var bytes=await worker.ExportAsync(wizard,canonical);using var document=Document.Open(bytes);
        Assert.AreEqual(5,document.Pages.Count);
        var pages=new List<string>();for(var i=4;i<=document.Pages.Count;i++){var text=new TextAbsorber();text.Visit(document.Pages[i]);pages.Add(text.Text);}
        var printable=string.Join("\n",pages);foreach(var spell in wizard.Play.Spells)StringAssert.Contains(printable,spell.Name);
        StringAssert.Contains(pages[1],"Complete spell list (continued)");
        Assert.AreEqual("ready",(await worker.InspectAsync(bytes)).Status);
        document.EmbeddedFiles!.Delete();var detached=await worker.InspectAsync(document.ToArray());
        Assert.AreEqual("review_required",detached.Status);Assert.AreEqual("exact",detached.TemplateMatch);Assert.AreEqual(334,detached.Fields.Count);
        Assert.AreEqual(11,detached.ExtractedData["spells"]!["by_level"]!["1"]!.AsArray().Count);
    }
    [TestMethod]
    public void ExcessivePrintableListFailsExplicitlyInsteadOfTruncating()
    {
        var error=Assert.Throws<PdfFormatException>(()=>PdfCodec.Export(Wizard(1500),new(){["schema_version"]=5,["name"]="Fictional excessive book",["level"]=4}));
        Assert.AreEqual("spell_appendix_too_large",error.Code);
    }
    [TestMethod]
    public void ExportDoesNotRetainObsoleteTemplateLinearizationHints()
    {
        using var document=Document.Open(PdfCodec.Export(Wizard(14),new(){["schema_version"]=5,["name"]="Fictional serialized book",["level"]=4}));
        Assert.IsFalse(document.IsLinearized,"Original template byte offsets must not be retained after filling fields and appending pages.");
    }
}
