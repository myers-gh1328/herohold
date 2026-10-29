using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class CreationChoiceTests
{
    private static CreationChoiceRules Rules()
    {
        const string cls="""[{"name":"Fighter","hit_die":10,"skill_choices":{"choose":2,"from":["Athletics","Insight","Perception"]},"class_options":{"fighting_style":["Defense"]},"progression":{"1":{"features":[]}}}]""";
        var cat=RulesCatalogSnapshot.Parse(cls,"""[{"name":"Human","traits":["Versatile"]}]""","""[{"name":"Guard","skill_proficiencies":["Athletics"],"origin_feats":["Alert"],"ability_options":{"abilities":["Strength","Intelligence","Wisdom"]}}]""","[]","[]","[]","[]","{}");
        var classes=ClassProgressionCatalog.Parse(cls);
        return new(cat,classes,new(classes,cat,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}")),RulesReference.Parse("""{"origin_feats":["Alert","Lucky","Magic Initiate (Wizard)"]}"""));
    }
    private static JsonObject Character(JsonObject options)=>new(){["name"]="Test",["class"]="Fighter",["species"]="Human",["background"]="Guard",["level"]=1,["class_options"]=options,["background_options"]=new JsonObject{["primary_ability"]="Strength",["secondary_ability"]="Strength"}};
    private static CreationChoiceContext Context(JsonObject character)=>CanonicalFixtures.Creation(character) with{ClassContext=new(){ClassName="Fighter"}};
    [TestMethod] public void InvalidRawSelectionsRemainVisibleAndBlockConfirmation()
    {
        var context=Context(Character(new(){["class_skills"]=new JsonArray("Insight","Insight")}));var before=System.Text.Json.JsonSerializer.Serialize(context.Build);var report=Rules().Describe(context);
        var skills=report.Definitions.Single(d=>d.Id=="class_skills");Assert.AreEqual(BuildChoiceStatus.Unresolved,skills.Status);Assert.AreEqual(2,skills.PreservedSelection!.Items!.Count);
        Assert.IsFalse(skills.Options.Any(o=>o.Label=="Athletics"));Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(context.Build));
        Assert.AreEqual(BuildChoiceStatus.Unresolved,report.Definitions.Single(d=>d.Id=="secondary_ability").Status);
    }
    [TestMethod] public void BackgroundOwnedOriginIsDisabledAndAbsentIdentityIsRepairable()
    {
        var context=Context(Character(new(){["class_skills"]=new JsonArray("Insight","Perception")}));var report=Rules().Describe(context);
        Assert.IsTrue(report.Definitions.Single(d=>d.Id=="species_origin_feats").Options.Single(o=>o.Label=="Alert").Disabled);
        var empty=Rules().Describe(new());Assert.IsTrue(empty.Issues.Count>0);Assert.IsTrue(empty.Definitions.Any(d=>d.Id=="class"));
    }
    [TestMethod] public void ReturnedSelectionsAndMetadataDoNotAliasInput()
    {
        var context=Context(Character(new(){["class_skills"]=new JsonArray("Insight","Perception")}));var report=Rules().Describe(context);
        report.Definitions.Single(d=>d.Id=="class_skills").PreservedSelection!.Items!.Clear();
        Assert.AreEqual(2,context.Build.ClassOptions!["class_skills"].Items!.Count);
    }
    [TestMethod] public void OriginSpellSelectionsKeepSeparateSourcesAndRejectIncompleteChoices()
    {
        var character=Character(new());character["species_options"]=new JsonObject{["origin_feats"]=new JsonArray("Magic Initiate (Wizard)")};var context=Context(character);
        var result=Rules().Describe(context);var cantrips=result.Definitions.Single(d=>d.Id=="origin_Wizard_species_cantrips");
        CollectionAssert.AreEqual(new[]{"origin_feat_choices","cantrips"},cantrips.Path.ToArray());Assert.AreEqual(2,cantrips.Choose);Assert.AreEqual(BuildChoiceStatus.Unresolved,cantrips.Status);
        Assert.IsTrue(result.Definitions.Any(d=>d.Id=="origin_Wizard_species_ability"));
    }
    [TestMethod] public void ForgedBackgroundOriginDoesNotAuthorizeSpellChoices()
    {
        var character=Character(new());character["background_options"]!["origin_feats"]=new JsonArray("Magic Initiate (Wizard)");var context=Context(character);
        var result=Rules().Describe(context);Assert.IsFalse(result.Definitions.Any(d=>d.Id=="origin_Wizard_cantrips"));Assert.IsTrue(result.Issues.Any(i=>i.Code=="unsupported_origin_source"));
        Assert.AreEqual("Magic Initiate (Wizard)",ChoiceReading.Names(context.Build.BackgroundOptions!["origin_feats"]).Single());
    }
    [TestMethod] public void OrdinaryCantripsExcludeClassOwnedOrderCantripsButNotIndependentMagicInitiateSources()
    {
        const string cls="""[{"name":"Druid","hit_die":8,"class_options":{"primal_order":["Magician"]},"spellcasting":{"ability":"Wisdom","spell_list":"Druid","cantrips_known":2},"progression":{"1":{"features":[]}}}]""";
        var catalog=RulesCatalogSnapshot.Parse(cls,"[]","[]","[]","""[{"name":"Guidance","level":0,"classes":["Druid"]},{"name":"Druidcraft","level":0,"classes":["Druid"]}]""","[]","[]","{}");var classes=ClassProgressionCatalog.Parse(cls);
        var choices=new CreationChoiceRules(catalog,classes,new(classes,catalog,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}")),new());
        var context=CanonicalFixtures.Creation(new(){["class"]="Druid",["level"]=1,["class_options"]=new JsonObject{["primal_order"]="Magician",["extra_cantrips"]=new JsonArray("Guidance")}}) with{ClassContext=new(){ClassName="Druid",CantripSources=[new("Guidance","class_options:extra_cantrips",["Druid"])]}};
        Assert.IsFalse(choices.Describe(context).Definitions.Single(d=>d.Id=="spells_cantrips").Options.Any(o=>o.Label=="Guidance"));
        var independent=context with{ClassContext=context.ClassContext with{CantripSources=[new("Guidance","origin:Magic Initiate",[])]}};
        Assert.IsTrue(choices.Describe(independent).Definitions.Single(d=>d.Id=="spells_cantrips").Options.Any(o=>o.Label=="Guidance"));
    }
}
