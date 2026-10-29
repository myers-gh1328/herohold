using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")]
[TestClass]
public sealed class SelectableFeatCatalogTests
{
    private static string Root()
    { var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/raw/5etools/feats.json")))root=root.Parent;return root!.FullName; }
    private static NativeSelectableFeatCatalog Additional()=>NativeSelectableFeatCatalog.Parse(File.ReadAllText(Path.Combine(Root(),"data/raw/5etools/feats.json")));
    private static FeatSelectionRules Rules()
    {
        var root=Root();string Read(string name)=>File.ReadAllText(Path.Combine(root,"data/imported/5etools",name+".json"));
        var catalog=RulesCatalogSnapshot.Parse(Read("classes"),Read("species"),Read("backgrounds"),Read("feats"),Read("spells"),Read("equipment"),Read("weapons"),File.ReadAllText(Path.Combine(root,"data/raw/5etools/spells/spells-xphb.json")));
        return new(catalog,Additional().AdditionalDefinitions,ToolCatalog.Parse(File.ReadAllText(Path.Combine(root,"data/raw/5etools/items-base.json")),File.ReadAllText(Path.Combine(root,"data/raw/5etools/items.json"))));
    }
    [TestMethod] public void MagicInitiateListsAreIndependentAndRequiredChildrenUseTheirActualClass()
    {
        var rules=Rules();var context=new FeatRuleContext{Level=4,OwnedFeats=[new("background","magic_initiate_wizard_xphb","Magic Initiate (Wizard)",1,new Dictionary<string,ChoiceValue>())]};
        Assert.IsFalse(rules.Evaluate("magic_initiate_wizard_xphb",context).Selectable);Assert.IsTrue(rules.Evaluate("magic_initiate_cleric_xphb",context).Selectable);
        var fields=rules.DescribeChoices("magic_initiate_cleric_xphb",context);Assert.AreEqual(3,fields.Count);Assert.AreEqual(2,fields.Single(f=>f.Key=="cantrips").Choose);
        Assert.IsTrue(fields.Single(f=>f.Key=="cantrips").Options.Contains("Guidance"));Assert.IsFalse(fields.Single(f=>f.Key=="cantrips").Options.Contains("Fire Bolt"));
        var choices=new JsonObject{["spellcasting_ability"]="Wisdom",["cantrips"]=new JsonArray("Guidance","Sacred Flame"),["spell"]="Bless"};
        Assert.IsTrue(rules.ValidateSelection("magic_initiate_cleric_xphb",context,new JsonObject{["choices"]=choices}).Valid);
        choices["cantrips"]=new JsonArray("Guidance","Fire Bolt");Assert.IsFalse(rules.ValidateSelection("magic_initiate_cleric_xphb",context,new JsonObject{["choices"]=choices.DeepClone()}).Valid);
    }
    [TestMethod] public void ToolsAreConcreteAndSkilledRepeatabilityDoesNotCollapseOwners()
    {
        var rules=Rules();var context=new FeatRuleContext{Level=4,OwnedFeats=[new("background","skilled_xphb","Skilled",1,new Dictionary<string,ChoiceValue>())]};Assert.IsTrue(rules.Evaluate("skilled_xphb",context).Selectable);
        var choices=rules.DescribeChoices("skilled_xphb",context).Single();Assert.AreEqual(3,choices.Choose);Assert.IsTrue(choices.Options.Contains("Tool: Lute"));Assert.IsTrue(choices.Options.Contains("Skill: Insight"));Assert.IsFalse(choices.Options.Any(o=>o.Contains("Any artisan")));
        Assert.IsTrue(rules.ValidateSelection("skilled_xphb",context,new JsonObject{["choices"]=new JsonObject{["proficiencies"]=new JsonArray("Skill: Insight","Tool: Lute","Tool: Smith's Tools")}}).Valid);
        Assert.AreEqual(3,rules.DescribeChoices("crafter_xphb",context).Single().Choose);Assert.AreEqual(3,rules.DescribeChoices("musician_xphb",context).Single().Choose);
    }
    [TestMethod] public void FightingStylesRequireFeatureAndCannotRepeatAlreadyOwnedStyles()
    {
        var rules=Rules();Assert.IsFalse(rules.Evaluate("defense_xphb",new(){Level=4}).Selectable);
        var context=new FeatRuleContext{Level=4,Features=["Fighting Style"],OwnedFeats=[new("class_options:fighting_style","defense_xphb","Defense",1,new Dictionary<string,ChoiceValue>())]};
        Assert.IsFalse(rules.Evaluate("defense_xphb",context).Selectable);Assert.IsTrue(rules.Evaluate("archery_xphb",context).Selectable);
    }
    [TestMethod] public void ActualSupplementHasTwelveOriginVariantsAndTenStylesWithoutChangingPublicCatalog()
    {
        var extras=Additional().AdditionalDefinitions;Assert.AreEqual(22,extras.Count);Assert.AreEqual(12,extras.Count(f=>f.Category=="origin"));Assert.AreEqual(10,extras.Count(f=>f.Category=="fighting_style"));
        Assert.AreEqual(22,extras.Select(f=>f.Id).Distinct().Count());
        CollectionAssert.AreEquivalent(new[]{"Magic Initiate (Cleric)","Magic Initiate (Druid)","Magic Initiate (Wizard)"},extras.Where(f=>f.MagicInitiateClass.Length>0).Select(f=>f.Name).ToArray());
        Assert.IsFalse(extras.Any(f=>f.Name is "Blessed Warrior" or "Druidic Warrior"));
        Assert.IsTrue(extras.Single(f=>f.Name=="Skilled").Repeatable);
        Assert.IsFalse(extras.Single(f=>f.Name=="Magic Initiate (Wizard)").Repeatable);
    }
    [TestMethod] public void SupplementReadsAreDetachedAndMalformedInputFailsClosed()
    {
        var catalog=Additional();Assert.IsNotInstanceOfType<List<CatalogFeat>>(catalog.AdditionalDefinitions,"The supplement cannot be changed through its list.");
        Assert.ThrowsExactly<ArgumentException>(()=>NativeSelectableFeatCatalog.Parse("[]"));
        Assert.ThrowsExactly<ArgumentException>(()=>NativeSelectableFeatCatalog.Parse("{\"feat\":{}}"));
        Assert.ThrowsExactly<ArgumentException>(()=>NativeSelectableFeatCatalog.Parse("{\"feat\":[{\"source\":\"XPHB\",\"category\":\"FS\",\"name\":\"Unknown\",\"prerequisite\":[{\"unsupported\":true}]}]}"));
    }
}
