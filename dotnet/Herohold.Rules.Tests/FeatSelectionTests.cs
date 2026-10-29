using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class FeatSelectionTests
{
    private static FeatSelectionRules Rules(string feats)=>new(RulesCatalogSnapshot.Parse("[]","[]","[]",feats,"[]","[]","[]","{}"));
    private const string Catalog="""[{"id":"actor","name":"Actor","prerequisites":{"any":[{"minimum_level":4,"ability_scores":[{"Charisma":13}]}]},"ability_increase":{"mode":"fixed","from":["Charisma"],"choose":1,"amount":1}},{"id":"elemental","name":"Elemental Adept","repeatable":true,"prerequisites":{"any":[{"minimum_level":4,"spellcasting":true}]}},{"id":"skill","name":"Skill Expert","prerequisites":{"any":[{}]}},{"id":"resilient","name":"Resilient","prerequisites":{"any":[{}]},"ability_increase":{"mode":"choose","from":["Strength","Dexterity","Constitution","Intelligence","Wisdom","Charisma"],"choose":1,"amount":1}},{"id":"boon","name":"Boon of Energy Resistance","prerequisites":{"any":[{"minimum_level":19}]}}]""";
    [TestMethod] public void PrerequisitesAndOwnedInstancesAreCheckedWithoutMutatingContext()
    {
        var rules=Rules(Catalog);var context=new FeatRuleContext{Level=4,Scores=new Dictionary<string,int>{{"Charisma",13}}};
        Assert.IsTrue(rules.Evaluate("actor",context).Eligible);
        Assert.IsFalse(rules.Evaluate("actor",context with{Level=3}).Eligible);
        Assert.IsFalse(rules.Evaluate("actor",context with{OwnedFeats=[new("a","actor","Actor",4,new Dictionary<string,ChoiceValue>())]}).Eligible);
        Assert.IsTrue(rules.Evaluate("actor",context with{EditingSourceId="a",OwnedFeats=[new("a","actor","Actor",4,new Dictionary<string,ChoiceValue>())]}).Eligible);
        Assert.IsFalse(rules.Evaluate("elemental",context).Eligible);
        Assert.IsFalse(rules.Evaluate("missing",context).Eligible);
    }
    [TestMethod] public void RepeatedElementalChoicesExcludeOtherInstancesButRetainOwn()
    {
        var context=new FeatRuleContext{Level=8,HasSpellcastingFeature=true,EditingSourceId="second",OwnedFeats=[new("first","elemental","Elemental Adept",4,new Dictionary<string,ChoiceValue>{["damage_type"]=ChoiceValue.Of("Fire")}),new("second","elemental","Elemental Adept",8,new Dictionary<string,ChoiceValue>{["damage_type"]=ChoiceValue.Of("Cold")})]};
        var options=Rules(Catalog).DescribeChoices("elemental",context).Single().Options;
        Assert.IsFalse(options.Contains("Fire"));Assert.IsTrue(options.Contains("Cold"));
        Assert.AreEqual("Cold",context.OwnedFeats[1].Choices["damage_type"].Text);
    }
    [TestMethod] public void SkillExpertCanGiveExpertiseInItsNewSkillButCannotReuseAnotherSourceExpertise()
    {
        var rules=Rules(Catalog);var context=new FeatRuleContext{ProficiencySources=[new("background",ProficiencySourceKind.Skill,"Insight"),new("class",ProficiencySourceKind.Expertise,"Insight")]};
        Assert.IsTrue(rules.ValidateSelection("skill",context,JsonNode.Parse("""{"choices":{"skill_proficiency":"Perception","expertise":"Perception"}}""")!.AsObject()).Valid);
        Assert.IsFalse(rules.ValidateSelection("skill",context,JsonNode.Parse("""{"choices":{"skill_proficiency":"Perception","expertise":"Insight"}}""")!.AsObject()).Valid);
    }
    [TestMethod] public void MultiSelectionRejectsDuplicatesWrongCountsAndWrongShapes()
    {
        var rules=Rules(Catalog);var context=new FeatRuleContext{Level=19};
        foreach(var json in new[]{"""{"choices":{"damage_types":["Fire","Fire"]}}""","""{"choices":{"damage_types":["Fire"]}}""","""{"choices":{"damage_types":"Fire"}}"""}) Assert.IsFalse(rules.ValidateSelection("boon",context,JsonNode.Parse(json)!.AsObject()).Valid);
        Assert.IsTrue(rules.ValidateSelection("boon",context,JsonNode.Parse("""{"choices":{"damage_types":["Fire","Cold"]}}""")!.AsObject()).Valid);
    }
    [TestMethod] public void AbilityAllocationMustMatchCatalogAndResilientCannotChooseExistingSave()
    {
        var rules=Rules(Catalog);var context=new FeatRuleContext{Level=4,Scores=new Dictionary<string,int>{{"Charisma",13}},ProficiencySources=[new("class",ProficiencySourceKind.Save,"Wisdom")]};
        Assert.IsTrue(rules.ValidateSelection("actor",context,JsonNode.Parse("""{"ability_increases":{"Charisma":1}}""")!.AsObject()).Valid);
        Assert.IsFalse(rules.ValidateSelection("actor",context,JsonNode.Parse("""{"ability_increases":{"Strength":1}}""")!.AsObject()).Valid);
        Assert.IsFalse(rules.ValidateSelection("resilient",context,JsonNode.Parse("""{"ability_increases":{"Wisdom":1}}""")!.AsObject()).Valid);
        Assert.IsTrue(rules.ValidateSelection("resilient",context,JsonNode.Parse("""{"ability_increases":{"Constitution":1}}""")!.AsObject()).Valid);
    }
    [TestMethod] public void PrerequisiteEligibleFeatWithNoRemainingChoiceIsNotSelectable()
    {
        var context=new FeatRuleContext{Level=20,HasSpellcastingFeature=true,OwnedFeats=new[]{"Acid","Cold","Fire","Lightning","Thunder"}.Select((type,index)=>new OwnedFeatSelection(index.ToString(),"elemental","Elemental Adept",4,new Dictionary<string,ChoiceValue>{["damage_type"]=ChoiceValue.Of(type)})).ToArray()};
        var verdict=Rules(Catalog).Evaluate("elemental",context);Assert.IsTrue(verdict.Eligible);Assert.IsFalse(verdict.Selectable);Assert.IsTrue(verdict.Issues.Any(i=>i.Code=="no_available_choices"));
    }
    [TestMethod] public void RitualChoicesUseStructuredMetadataIncludingNewCatalogEntries()
    {
        var catalog=RulesCatalogSnapshot.Parse("[]","[]","[]","""[{"id":"ritual","name":"Ritual Caster","prerequisites":{"any":[{}]}}]""","""[{"name":"Fixture Ritual","level":1,"source":"XPHB"},{"name":"Wrong Level","level":2,"source":"XPHB"}]""","[]","[]","""{"spell":[{"name":"Fixture Ritual","source":"XPHB","meta":{"ritual":true}},{"name":"Wrong Level","source":"XPHB","meta":{"ritual":true}}]}""");
        CollectionAssert.AreEqual(new[]{"Fixture Ritual"},new FeatSelectionRules(catalog).DescribeChoices("ritual",new()).Single().Options.ToArray());
    }
}
