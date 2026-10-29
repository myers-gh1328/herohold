using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class CharacterEditTests
{
    private static CharacterEditRules Rules()=>new(RulesCatalogSnapshot.Parse("[]","""[{"name":"Human","traits":["Versatile"]}]""","""[{"name":"Wizard Background","origin_feats":["Magic Initiate (Wizard)"]},{"name":"Druid Background","origin_feats":["Magic Initiate (Druid)"]}]""","[]","[]","[]","[]","{}"));
    [TestMethod] public void SwitchingOriginSourceDoesNotAssignItsSpellsToAnotherFeatAndRestoresOnReturn()
    {
        var c=CanonicalCharacterRules.Create();c["background"]="Wizard Background";c["origin_feat_choices"]!["spell"]="Shield";c["origin_feat_choices"]!["cantrips"]=new JsonArray("Light","Mage Hand");
        var changed=Rules().Apply(c,new(){["background"]="Druid Background"});Assert.IsFalse(changed["origin_feat_choices"]!.AsObject().ContainsKey("spell"));
        var restored=Rules().Apply(changed,new(){["background"]="Wizard Background"});Assert.AreEqual("Shield",restored["origin_feat_choices"]!["spell"]!.GetValue<string>());Assert.AreEqual("Wizard Background",c["background"]!.GetValue<string>());
    }
    [TestMethod] public void ParentChoiceChangeKeepsRawChildForRepairWithoutMutatingSource()
    {
        var c=CanonicalCharacterRules.Create();c["class_options"]!["fighting_style"]="Blessed Warrior";c["class_options"]!["blessed_warrior_cantrips"]=new JsonArray("Light","Guidance");
        var updated=Rules().Apply(c,new(){["class_options"]=new JsonObject{["fighting_style"]="Defense"}});
        Assert.AreEqual(2,updated["class_options"]!["blessed_warrior_cantrips"]!.AsArray().Count);Assert.AreEqual("Blessed Warrior",c["class_options"]!["fighting_style"]!.GetValue<string>());
    }
    [TestMethod] public void ClassChangesAtLevelOneClearClassOwnedSelectionsAndLaterChangesAreRejected()
    {
        var c=CanonicalCharacterRules.Create();c["class"]="Fighter";c["subclass"]="Champion";c["class_options"]!["fighting_style"]="Defense";
        var updated=Rules().Apply(c,new(){["class"]="Wizard"});Assert.AreEqual(0,updated["class_options"]!.AsObject().Count);Assert.AreEqual("",updated["subclass"]!.GetValue<string>());
        c["level"]=5;Assert.Throws<ArgumentException>(()=>Rules().Apply(c,new(){["class"]="Wizard"}));
    }
    [TestMethod] public void MixedSkilledChoicesSplitIntoOwnedSkillAndToolLists()
    {
        var c=CanonicalCharacterRules.Create();var updated=Rules().Apply(c,new(){["origin_feat_choices"]=new JsonObject{["proficiencies"]=new JsonArray("Skill: Insight","Skill: Perception","Tool: Any artisan's tools")}});
        Assert.AreEqual(2,updated["origin_feat_choices"]!["skills"]!.AsArray().Count);Assert.AreEqual(1,updated["origin_feat_choices"]!["tools"]!.AsArray().Count);Assert.IsFalse(updated["origin_feat_choices"]!.AsObject().ContainsKey("proficiencies"));
        Assert.Throws<ArgumentException>(()=>Rules().Apply(c,new(){["origin_feat_choices"]=new JsonObject{["proficiencies"]=new JsonArray("Anything")}}));
    }
    [TestMethod] public void FirstLevelClassSwitchClearsOldProgressionWithoutHealingOrDroppingManualHpAdjustment()
    {
        var c=CanonicalCharacterRules.Create();c["class"]="Fighter";c["class_levels"]=new JsonObject{["Fighter"]=1};
        c["hp_history"]=new JsonArray(new JsonObject{["character_level"]=1,["class_name"]="Fighter",["method"]="maximum",["base_gain"]=10});
        c["hit_points"]=new JsonObject{["current"]=3,["temporary"]=2,["maximum"]=15,["entries"]=new JsonArray(new JsonObject{["class_name"]="Fighter"})};
        c["import_overrides"]!["maximum_hp_adjustment"]=5;
        var updated=Rules().Apply(c,new(){["class"]="Wizard"});
        Assert.AreEqual(1,updated["class_levels"]!["Wizard"]!.GetValue<int>());Assert.IsFalse(updated["class_levels"]!.AsObject().ContainsKey("Fighter"));
        Assert.AreEqual(0,updated["hp_history"]!.AsArray().Count);
        Assert.AreEqual(3,updated["hit_points"]!["current"]!.GetValue<int>());Assert.AreEqual(2,updated["hit_points"]!["temporary"]!.GetValue<int>());Assert.AreEqual(5,updated["import_overrides"]!["maximum_hp_adjustment"]!.GetValue<int>());
        Assert.AreEqual("Fighter",c["class"]!.GetValue<string>());Assert.AreEqual(1,c["hp_history"]!.AsArray().Count);
    }
    [TestMethod] public void UnsupportedTopLevelEditsAndWrongKnownShapesAreRejected()
    {
        var c=CanonicalCharacterRules.Create();Assert.Throws<ArgumentException>(()=>Rules().Apply(c,new(){["level"]=20}));Assert.Throws<ArgumentException>(()=>Rules().Apply(c,new(){["class_options"]=new JsonArray()}));
    }
    [TestMethod] public void ExplicitBuildChoiceEditUpdatesItsCurrentConfiguredValue()
    {
        var c=CanonicalCharacterRules.Create();c["class_options"]!["hunters_prey"]="Colossus Slayer";c["feature_configurations"]=new JsonArray(new JsonObject{["feature_id"]="hunters_prey",["current_value"]="Horde Breaker"});
        var changed=Rules().Apply(c,new(){["class_options"]=new JsonObject{["hunters_prey"]="Colossus Slayer"}});
        Assert.AreEqual("Colossus Slayer",changed["feature_configurations"]![0]!["current_value"]!.GetValue<string>());Assert.AreEqual("Horde Breaker",c["feature_configurations"]![0]!["current_value"]!.GetValue<string>());
    }
}
