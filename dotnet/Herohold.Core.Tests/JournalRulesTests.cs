using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class JournalRulesTests
{
    private static CharacterState Character()=>Fixtures.Ranger() with{Build=new(){Background="Sailor"},Notes=[new(){Id="legacy",Title="Old note",Text="Retained story",CreatedAt=DateTimeOffset.Parse("2024-01-02T00:00:00Z")} ]};
    [TestMethod] public void LegacyNotesDefaultToNotesAndCategorizedChangesStayDetached()
    {
        var original=Character();Assert.AreEqual(JournalCategory.Notes,JournalRules.Entries(original).Single().Category);
        var added=JournalRules.Add(original,"Find the scout","Meet at the bridge",JournalCategory.Quests);Assert.AreEqual(1,original.Notes.Count);Assert.AreEqual(2,added.Notes.Count);Assert.AreEqual("Sailor",added.Build!.Background,"Journal changes keep the rest of the build.");
        var entry=JournalRules.Entries(added).Single(e=>e.Note.Title=="Find the scout");Assert.AreEqual(JournalCategory.Quests,entry.Category);Assert.AreNotEqual("legacy",entry.Note.Id);
        var moved=JournalRules.Move(added,"legacy",JournalCategory.Locations);Assert.AreEqual(JournalCategory.Notes,JournalRules.Entries(added).Single(e=>e.Note.Id=="legacy").Category);Assert.AreEqual(JournalCategory.Locations,JournalRules.Entries(moved).Single(e=>e.Note.Id=="legacy").Category);
        var edited=JournalRules.Edit(moved,"legacy","The bridge","Ask the friendly scout",JournalCategory.Friends);Assert.AreEqual("legacy",edited.Notes[0].Id);Assert.AreEqual(original.Notes[0].CreatedAt,edited.Notes[0].CreatedAt);Assert.AreEqual("Old note",original.Notes[0].Title);Assert.AreEqual(JournalCategory.Friends,JournalRules.Entries(edited).Single(e=>e.Note.Id=="legacy").Category);
        Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(edited.Notes[0]).Contains("Category"),"JournalNote's strict persisted shape must stay unchanged.");
    }
    [TestMethod] public void JournalEditsDoNotSpendResourcesChangeHealthOrInvalidateTheLastPlayUndo()
    {
        var original=Character();var result=JournalRules.Edit(original,"legacy","A friend","Remember Rowan",JournalCategory.Friends);
        Assert.AreEqual(original.CurrentHp,result.CurrentHp);Assert.AreEqual(original.Resources[0].Remaining,result.Resources[0].Remaining);Assert.AreEqual(original.Play.UndoHistory.Count,result.Play.UndoHistory.Count);
        result.Notes[0]=result.Notes[0] with{Text="Changed clone"};Assert.AreEqual("Retained story",original.Notes[0].Text);
    }
    [TestMethod] public void MissingIdentityAndUnsupportedCategoryCannotModifyAnyEntry()
    {
        var original=Character();Assert.Throws<ArgumentException>(()=>JournalRules.Move(original,"missing",JournalCategory.Foes));Assert.Throws<ArgumentException>(()=>JournalRules.Move(original,"legacy",(JournalCategory)99));Assert.Throws<ArgumentException>(()=>JournalRules.Edit(original,"legacy","", "text",JournalCategory.Notes));Assert.AreEqual("Old note",original.Notes[0].Title);
    }
    [TestMethod]
    [DataRow("{\"version\":2,\"categories\":{}}",false)]
    [DataRow("{\"version\":1,\"categories\":{\"legacy\":\"Unknown\"}}",false)]
    [DataRow("{\"version\":1,\"categories\":{\"legacy\":7}}",true)]
    [DataRow("{\"version\":1,\"categories\":{},\"unexpected\":true}",true)]
    public void MalformedOwnedMetadataIsRejectedWithoutErasingIt(string json,bool refusedWhenRead)
    {
        var document=new JsonObject{["folio_journal"]=JsonNode.Parse(json)};
        if(refusedWhenRead){Assert.Throws<ArgumentException>(()=>BuildFacts.FromCanonical(document),"A shape the build cannot hold is refused when read.");return;}
        var c=Character() with{Build=BuildFacts.FromCanonical(document)};Assert.Throws<ArgumentException>(()=>JournalRules.ValidateMetadata(c));Assert.AreEqual(json,c.Build!.ToCanonical(c.Id)["folio_journal"]!.ToJsonString());
    }
    [TestMethod] public void CustomGroupsCanBeEmptyAndTheirAssignmentsSurviveEditsAndMovesDetached()
    {
        var original=Character();var grouped=JournalRules.CreateCategory(original,"  Our caravan  ");
        CollectionAssert.Contains(JournalRules.CategoryNames(grouped).ToArray(),"Our caravan");Assert.IsNull(original.Build!.CustomJournal);
        var moved=JournalRules.Move(grouped,"legacy","our CARAVAN");var named=JournalRules.NamedEntries(moved).Single();Assert.AreEqual("Our caravan",named.Category);Assert.AreEqual(original.Notes[0],named.Note);
        Assert.AreEqual(JournalCategory.Notes,JournalRules.Entries(moved).Single().Category,"Old readers need a Notes fallback.");
        var edited=JournalRules.Edit(moved,"legacy","A contact","Keep this", "Our caravan");Assert.AreEqual("Our caravan",JournalRules.NamedEntries(edited).Single().Category);Assert.AreEqual(original.Notes[0].CreatedAt,edited.Notes[0].CreatedAt);
        var back=JournalRules.Move(edited,"legacy",JournalCategory.Friends);Assert.AreEqual("Friends",JournalRules.NamedEntries(back).Single().Category);Assert.AreEqual("Our caravan",JournalRules.NamedEntries(moved).Single().Category);
        var added=JournalRules.Add(back,"Supply list","Oil and rope","Our caravan");Assert.AreEqual("Our caravan",JournalRules.NamedEntries(added).Single(e=>e.Note.Title=="Supply list").Category);Assert.AreEqual("Sailor",added.Build!.Background,"Journal changes keep the rest of the build.");
    }
    [TestMethod]
    [DataRow("")][DataRow("   ")][DataRow("quests")][DataRow(" FRIENDS ")][DataRow("Our caravan")][DataRow("A category name that exceeds the forty eight character limit")]
    public void InvalidOrDuplicateCustomNamesAreRejected(string name)
    {var c=JournalRules.CreateCategory(Character(),"Our caravan");Assert.Throws<ArgumentException>(()=>JournalRules.CreateCategory(c,name));Assert.AreEqual(7,JournalRules.CategoryNames(c).Count);}
    [TestMethod] public void CustomMetadataRejectsUnknownAssignmentsDuplicatesAndLimits()
    {
        foreach(var json in new[]{"{\"version\":2,\"categories\":[],\"notes\":{}}","{\"version\":1,\"categories\":[\"Camp\",\"camp\"],\"notes\":{}}","{\"version\":1,\"categories\":[\"Camp\"],\"notes\":{\"legacy\":\"Missing\"}}"})
        {var c=Character() with{Build=BuildFacts.FromCanonical(new JsonObject{["folio_journal_custom"]=JsonNode.Parse(json)})};Assert.Throws<ArgumentException>(()=>JournalRules.ValidateMetadata(c));}
        var bounded=Character();for(var i=0;i<50;i++)bounded=JournalRules.CreateCategory(bounded,"Group "+i);Assert.Throws<ArgumentException>(()=>JournalRules.CreateCategory(bounded,"Too many"));
        Assert.Throws<ArgumentException>(()=>JournalRules.Move(bounded,"legacy","Unregistered"));
    }
}
