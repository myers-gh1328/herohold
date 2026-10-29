using Herohold.Web.Services;
using System.Text.Json.Nodes;
using Bunit;
using Herohold.Web.Components.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;

[TestClass]
public sealed class ImportReviewSummaryTests
{
    [TestMethod]
    public void FieldsLeftOutOfAnUploadAreNamedOnlyWhenThereAreAny()
    {
        using var context = new BunitContext();
        var review = new ImportReviewView { CanAdopt = true, SourceKind = "json", DroppedFields = ["campaign_notes_v9", "homebrew_blessing"] };
        var cut = context.Render<ImportReviewSummary>(p => p.Add(x => x.Review, review));
        StringAssert.Contains(cut.Find("[data-testid='import-dropped-fields']").TextContent, "campaign_notes_v9, homebrew_blessing");
        var clean = context.Render<ImportReviewSummary>(p => p.Add(x => x.Review, new ImportReviewView { CanAdopt = true, SourceKind = "json" }));
        Assert.AreEqual(0, clean.FindAll("[data-testid='import-dropped-fields']").Count);
    }
}
