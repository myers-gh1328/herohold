using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass] public sealed class ClassResourceTests
{
    private static ClassResourceRules Rules()=>new(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")));
    private static NativeBuildSummary Build(string name,int level,int wisdom=4,int intelligence=3,int charisma=5)
    {
        var modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>new Calculation(ability==Ability.Wisdom?wisdom:ability==Ability.Intelligence?intelligence:ability==Ability.Charisma?charisma:2,[]));
        return new(){ClassName=name,Standing=new(){Modifiers=modifiers}};
    }
    [TestMethod] public void FighterAndMonkResourcesUseNativeClassThresholdsAndPartialRecovery()
    {
        var fighter=Rules().Derive(new JsonObject{["class"]="Fighter",["level"]=17},Build("Fighter",17),new());
        Assert.AreEqual(2,fighter.DerivedResources["action_surge_uses"].Number);Assert.AreEqual(3,fighter.DerivedResources["indomitable_uses"].Number);
        Assert.AreEqual(1,fighter.Metadata.Single(pool=>pool.Id=="second-wind").ShortRestRecoveryAmount);
        var monk=Rules().Derive(new JsonObject{["class"]="Monk",["level"]=5},Build("Monk",5),new());Assert.AreEqual(5,monk.Metadata.Single(pool=>pool.Id=="focus").Maximum);
    }
    [TestMethod] public void WarlockSlotsAndBardAbilityResourcesStayDistinctAndRecoveryIsExplicit()
    {
        var warlock=Rules().Derive(new JsonObject{["class"]="Warlock",["level"]=11},Build("Warlock",11),new(){Features=["Magical Cunning"]});
        var pact=warlock.Metadata.Single(pool=>pool.Id=="pact");Assert.AreEqual(5,pact.SlotLevel);Assert.AreEqual(3,pact.Maximum);Assert.AreEqual("short_or_long_rest",pact.Recovery);
        var bard=Rules().Derive(new JsonObject{["class"]="Bard",["level"]=5},Build("Bard",5,charisma:5),new(){OriginFeats=["Lucky"]});Assert.AreEqual(5,bard.Metadata.Single(pool=>pool.Id=="bardic-inspiration").Maximum);Assert.AreEqual(3,bard.Metadata.Single(pool=>pool.Id=="luck").Maximum);
    }
    [TestMethod] public void GloomHasOnlyOneStrikePoolAndHighLevelNamedFeaturesArePreserved()
    {
        var ranger=Rules().Derive(new JsonObject{["class"]="Ranger",["subclass"]="Gloom Stalker",["level"]=20,["speed"]=40},Build("Ranger",20),new());
        Assert.AreEqual(1,ranger.Metadata.Count(pool=>pool.Name=="Dreadful Strike"));Assert.AreEqual(4,ranger.Metadata.Single(pool=>pool.Id=="dread").Maximum);Assert.AreEqual("1d10",ranger.DerivedResources["hunters_mark_damage_die"].Text);Assert.AreEqual(40,ranger.DerivedResources["climb_speed"].Number);
        var wizard=Rules().Derive(new JsonObject{["class"]="Wizard",["subclass"]="Abjurer",["level"]=5},Build("Wizard",5),new(){Features=["Arcane Recovery"]});Assert.AreEqual(13,wizard.Metadata.Single(pool=>pool.Id=="arcane-ward").Maximum);Assert.AreEqual("manual",wizard.Metadata.Single(pool=>pool.Id=="arcane-ward").Recovery);
    }
}
