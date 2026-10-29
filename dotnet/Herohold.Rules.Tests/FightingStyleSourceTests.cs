using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class FightingStyleSourceTests
{
    [TestMethod]public void ClassStyleSourcesRequireTheirClassLevelAndActiveSubclassWhilePreservingRawSelections()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","subclass":"Champion","level":7,"class_options":{"fighting_style":"Dueling","champion_additional_fighting_styles":["Archery","Defense"]},"general_feats":[{"instance_id":"fictional-style","name":"Blind Fighting"}]}""")!.AsObject();
        Assert.AreEqual(4,CanonicalFixtures.Styles(raw).Count);var preserved=raw["class_options"]!["champion_additional_fighting_styles"]!.ToJsonString();
        foreach(var item in new[]{("Fighter","Battle Master",7),("Fighter","Champion",6),("Paladin","Champion",7),("Wizard","Champion",7)})
        {
            raw["class"]=item.Item1;raw["subclass"]=item.Item2;raw["level"]=item.Item3;
            Assert.IsFalse(CanonicalFixtures.Styles(raw).Any(source=>source.SourceId=="class_options:champion_additional_fighting_styles"));
            Assert.AreEqual(preserved,raw["class_options"]!["champion_additional_fighting_styles"]!.ToJsonString());Assert.IsTrue(CanonicalFixtures.ActiveStyles(raw).Contains("Blind Fighting"));
        }
        foreach(var item in new[]{("Fighter",1,true),("Paladin",1,false),("Paladin",2,true),("Ranger",1,false),("Ranger",2,true),("Wizard",20,false)})
        {raw["class"]=item.Item1;raw["level"]=item.Item2;Assert.AreEqual(item.Item3,CanonicalFixtures.Styles(raw).Any(source=>source.SourceId=="class_options:fighting_style"));}
        foreach(var item in new[]{("Paladin","Blessed Warrior",true),("Ranger","Blessed Warrior",false),("Ranger","Druidic Warrior",true),("Fighter","Druidic Warrior",false)})
        {raw["class"]=item.Item1;raw["level"]=2;raw["class_options"]!["fighting_style"]=item.Item2;Assert.AreEqual(item.Item3,CanonicalFixtures.Styles(raw).Any(source=>source.SourceId=="class_options:fighting_style"));}
    }
    [TestCategory("RulesData")][TestMethod]public void ClassAndPermanentFeatStylesRetainOwnersButTheirNumericalBenefitAppliesOnce()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":6,"class_options":{"fighting_style":"Archery"},"general_feats":[{"instance_id":"fictional-archery","name":"Archery"},{"instance_id":"fictional-defense","name":"Defense"}]}""")!.AsObject();
        Assert.AreEqual(2,CanonicalFixtures.Styles(raw).Count(source=>source.Name=="Archery"));Assert.AreEqual(1,CanonicalFixtures.ActiveStyles(raw).Count(name=>name=="Archery"));
        var build=new NativeBuildSummary{ClassName="Fighter",Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>new Calculation(ability==Ability.Dexterity?3:2,[]))}};var weapons=WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot());
        raw["class_options"]!["fighting_style"]="Defense";
        var attack=new AttackActionRules(weapons).Derive(raw,build,new(){Proficiencies=[new("class:Fighter",ProficiencySourceKind.Weapon,"Martial Weapons")]},new(),[new(){Id="bow",Name="Longbow",Equipped=true}]).Attacks.Single(a=>a.Kind=="weapon");Assert.AreEqual(2,attack.FlatBonus);Assert.AreEqual(8,attack.AttackBonus);
    }
    [TestCategory("RulesData")][TestMethod]public void UnarmedFightingAndTavernBrawlerProvideExplicitUnarmedDiceAndManualRiderReminders()
    {
        var weapons=WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot());var rules=new AttackActionRules(weapons);var build=new NativeBuildSummary{ClassName="Fighter",Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,_=>new Calculation(2,[]))}};
        var raw=new JsonObject{["class"]="Fighter",["level"]=4,["general_feats"]=new JsonArray(new JsonObject{["name"]="Unarmed Fighting",["instance_id"]="fictional-unarmed"})};
        var unarmed=rules.Derive(raw,build,new(),new(),[]).Attacks.Single(attack=>attack.Kind=="unarmed");Assert.AreEqual(8,unarmed.DamageDice!.Sides);Assert.AreEqual(Ability.Strength,unarmed.Ability);
        Assert.AreEqual(6,rules.Derive(raw,build,new(),new(),[new(){Id="shield",Name="Shield",Equipped=true}]).Attacks.Single(attack=>attack.Kind=="unarmed").DamageDice!.Sides);
        raw["general_feats"]=new JsonArray();var brawler=rules.Derive(raw,build,new(){OriginFeats=["Tavern Brawler"]},new(),[]).Attacks.Single(attack=>attack.Kind=="unarmed");Assert.AreEqual(4,brawler.DamageDice!.Sides);StringAssert.Contains(brawler.Notes,"manually");
    }
    [TestCategory("RulesData")][TestMethod]public void ConditionalFeatAndStyleDamageRulesAreExplicitManualRemindersRatherThanHiddenNumericBonuses()
    {
        var weapons=WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot());
        var build=new NativeBuildSummary{ClassName="Fighter",Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,_=>new Calculation(2,[]))}};
        var raw=new JsonObject{["level"]=4,["general_feats"]=new JsonArray(new JsonObject{["name"]="Great Weapon Fighting",["instance_id"]="fictional-gwf"})};
        var attack=new AttackActionRules(weapons).Derive(raw,build,new(){OriginFeats=["Savage Attacker"]},new(),[new(){Id="greatsword",Name="Greatsword",Equipped=true}]).Attacks.Single(a=>a.Kind=="weapon");
        StringAssert.Contains(attack.Notes,"Savage Attacker");StringAssert.Contains(attack.Notes,"Great Weapon Fighting");StringAssert.Contains(attack.Notes,"manually");Assert.AreEqual(2,attack.DamageModifier);
    }
}
