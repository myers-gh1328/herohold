using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class DependentResourceTests
{
    private static ClassProgressionCatalog Catalog()=>ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"classes.json")));
    private static Dictionary<Ability,int> Mods(int wisdom=4,int intelligence=3,int charisma=5)=>Enum.GetValues<Ability>().ToDictionary(a=>a,a=>a==Ability.Wisdom?wisdom:a==Ability.Intelligence?intelligence:a==Ability.Charisma?charisma:a==Ability.Dexterity?4:2);
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Bard","",5,"bardic_inspiration_uses",5)]
    [DataRow("Ranger","Gloom Stalker",5,"dreadful_strike_uses",4)]
    [DataRow("Ranger","Hunter",14,"natures_veil_uses",4)]
    [DataRow("Cleric","War Domain",3,"war_priest_uses",4)]
    [DataRow("Cleric","Light Domain",6,"warding_flare_uses",4)]
    [DataRow("Cleric","Light Domain",17,"corona_of_light_uses",4)]
    [DataRow("Wizard","Abjurer",5,"arcane_ward_max_hp",13)]
    [DataRow("Paladin","Oath of Glory",15,"glorious_defense_uses",5)]
    [DataRow("Fighter","Psi Warrior",15,"bulwark_of_force_targets",3)]
    public void NamedAbilityDependentResourcesUseCurrentNativeModifiers(string cls,string sub,int level,string key,int expected)
    {Assert.AreEqual(expected,DependentResourceRules.Calculate(Catalog(),cls,sub,level,Mods()).Values[key]);}
    [TestCategory("RulesData")][TestMethod] public void MinimumUseCountsThresholdsAndRecoveryAreExplicit()
    {
        var bard=DependentResourceRules.Calculate(Catalog(),"Bard","",5,Mods(charisma:-5));Assert.AreEqual(1,bard.Pools.Single().Maximum);Assert.AreEqual(RecoveryRest.Short,bard.Pools.Single().Recovery);
        Assert.IsFalse(DependentResourceRules.Calculate(Catalog(),"Ranger","Gloom Stalker",2,Mods()).Values.ContainsKey("dreadful_strike_uses"));
        Assert.AreEqual(RecoveryRest.Manual,DependentResourceRules.Calculate(Catalog(),"Wizard","Abjurer",3,Mods()).Pools.Single().Recovery);
        Assert.AreEqual(RecoveryRest.Short,DependentResourceRules.Calculate(Catalog(),"Cleric","Light Domain",6,Mods()).Pools.Single().Recovery);
    }
    [TestCategory("RulesData")][TestMethod] public void SpellAndManeuverDcsUseNativeAbilityAndPreserveExplicitAbilityChoice()
    {
        Assert.AreEqual(16,DependentResourceRules.Calculate(Catalog(),"Bard","",5,Mods()).Values["spell_save_dc"]);
        Assert.AreEqual(15,DependentResourceRules.Calculate(Catalog(),"Fighter","Battle Master",5,Mods()).Values["maneuver_save_dc"]);
        Assert.AreEqual(13,DependentResourceRules.Calculate(Catalog(),"Fighter","Battle Master",5,Mods(),Ability.Strength).Values["maneuver_save_dc"]);
        Assert.AreEqual(13,DependentResourceRules.Calculate(Catalog(),"Fighter","",5,Mods(),feats:["Shield Master"]).Values["shield_master_bash_dc"]);
    }
}
