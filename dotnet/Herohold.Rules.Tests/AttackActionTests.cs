using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass] public sealed class AttackActionTests
{
    private static AttackActionRules Rules()=>new(WeaponCapabilityCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),SpellGrantFixture.Snapshot()));
    private static NativeBuildSummary Build(string cls,int strength=1,int dexterity=4)=>new(){ClassName=cls,Standing=new(){Modifiers=Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>new Calculation(ability==Ability.Strength?strength:ability==Ability.Dexterity?dexterity:0,[]))}};
    private static ActiveFeatureSummary Features(params string[] weapons)=>new(){Proficiencies=weapons.Select(weapon=>new ChoiceProficiencySource("class:Fighter",ProficiencySourceKind.Weapon,weapon)).ToArray()};
    [TestMethod] public void OrdinaryCharactersAlwaysHaveAnUnarmedStrikeReference()
    {
        var result=Rules().Derive(new JsonObject{["class"]="Fighter",["level"]=5},Build("Fighter",strength:2),Features("Martial Weapons"),new(),[]);
        var attack=result.Attacks.SingleOrDefault(a=>a.Id=="unarmed");
        Assert.IsNotNull(attack,"An ordinary unarmed strike must exist even without a feat, style or Monk feature.");
        Assert.IsTrue(attack.Proficient);Assert.IsNull(attack.DamageDice);Assert.AreEqual(1,attack.FixedDamage);Assert.AreEqual(5,attack.AttackBonus);Assert.AreEqual(2,attack.DamageModifier);
        StringAssert.Contains(attack.Notes,"Grapple");StringAssert.Contains(attack.Notes,"Shove");StringAssert.Contains(attack.Notes,"DC 13");
    }
    [TestMethod] public void TrustedStaffAliasesRetainQuarterstaffMeleeAndMonkCapabilities()
    {
        foreach(var name in new[]{"Staff","Wooden Staff"})
        {
            var result=Rules().Derive(new JsonObject{["class"]="Monk",["level"]=5},Build("Monk"),Features("Simple Weapons"),new(){DerivedResources=new Dictionary<string,ChoiceValue>{["martial_arts_die"]=ChoiceValue.Of("1d8")}},[new(){Id="staff",Name=name,Equipped=true}]);
            var staff=result.Attacks.Single(attack=>attack.Name==name);Assert.AreEqual(Ability.Dexterity,staff.Ability);Assert.AreEqual(8,staff.DamageDice!.Sides);
        }
    }
    [TestMethod] public void WeaponMasterFeatShowsMasteryWithoutSpendingOrReplacingClassSelection()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":5,"weapon_masteries":["Longsword"],"general_feats":[{"name":"Weapon Master","choices":{"weapon_mastery":"Longbow"}}]}""")!.AsObject();
        var result=Rules().Derive(raw,Build("Fighter"),Features("Martial Weapons"),new(),[new(){Id="bow",Name="Longbow",Equipped=true},new(){Id="sword",Name="Longsword",Equipped=true}]);
        StringAssert.Contains(result.Attacks.Single(attack=>attack.Name=="Longbow").Notes,"Slow");StringAssert.Contains(result.Attacks.Single(attack=>attack.Name=="Longsword").Notes,"Sap");Assert.AreEqual(1,raw["weapon_masteries"]!.AsArray().Count);
    }
    [TestMethod] public void LongbowArcheryAndFinesseUseNativeModifiersAndActualProficiency()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":5,"class_options":{"fighting_style":"Archery"}}""")!.AsObject();
        var result=Rules().Derive(raw,Build("Fighter"),Features("Martial Weapons"),new(),[new(){Id="bow",Name="Longbow",Equipped=true},new(){Id="rapier",Name="Rapier",Equipped=true}]);
        var bow=result.Attacks.Single(attack=>attack.Name=="Longbow");Assert.AreEqual(9,bow.AttackBonus);Assert.AreEqual(4,bow.DamageModifier);Assert.AreEqual(Ability.Dexterity,bow.Ability);
        Assert.AreEqual(Ability.Dexterity,result.Attacks.Single(attack=>attack.Name=="Rapier").Ability);
    }
    [TestMethod] public void MonkMartialArtsUsesNativeDieAndStopsForArmorShieldOrNonMonkWeapons()
    {
        var raw=JsonNode.Parse("""{"class":"Monk","level":5}""")!.AsObject();var resources=new ClassResourceSummary{DerivedResources=new Dictionary<string,ChoiceValue>{["martial_arts_die"]=ChoiceValue.Of("1d8")}};
        EquipmentItem staff=new(){Id="staff",Name="Quarterstaff",Equipped=true};
        var result=Rules().Derive(raw,Build("Monk"),Features("Simple Weapons"),resources,[staff]);
        Assert.AreEqual(Ability.Dexterity,result.Attacks.Single(attack=>attack.Name=="Quarterstaff").Ability);Assert.AreEqual(8,result.Attacks.Single(attack=>attack.Name=="Quarterstaff").DamageDice!.Sides);Assert.AreEqual(8,result.Attacks.Single(attack=>attack.Kind=="unarmed").DamageDice!.Sides);
        foreach(var item in new[]{"Shield","Chain Mail","Longbow"})
        {
            result=Rules().Derive(raw,Build("Monk"),Features("Simple Weapons"),resources,[staff,new(){Id="blocker",Name=item,Equipped=true}]);
            Assert.AreEqual(Ability.Strength,result.Attacks.Single(attack=>attack.Name=="Quarterstaff").Ability);Assert.AreEqual(6,result.Attacks.Single(attack=>attack.Name=="Quarterstaff").DamageDice!.Sides);
        }
    }
    [TestMethod] public void VersatileAndDuelingAreExplicitAndCarriedWeaponsDoNotDeactivateDueling()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":5,"class_options":{"fighting_style":"Dueling"},"presentation_preferences":{"weapon_wielding_modes":{"Longsword":"two_handed"}}}""")!.AsObject();
        EquipmentItem sword=new(){Id="sword",Name="Longsword",Equipped=true};
        var result=Rules().Derive(raw,Build("Fighter",strength:4),Features("Martial Weapons"),new(),[sword]);Assert.AreEqual(10,result.Attacks.Single(a=>a.Kind=="weapon").DamageDice!.Sides);Assert.AreEqual(4,result.Attacks.Single(a=>a.Kind=="weapon").DamageModifier);
        raw["presentation_preferences"]!["weapon_wielding_modes"]!["Longsword"]="one_handed";
        result=Rules().Derive(raw,Build("Fighter",strength:4),Features("Martial Weapons"),new(),[sword,new(){Id="carried",Name="Dagger",Equipped=false}]);Assert.AreEqual(6,result.Attacks.Single(a=>a.Kind=="weapon").DamageModifier);
    }
    [TestMethod] public void ANamedWeaponAttacksAsItsBaseWeaponWithItsOwnNameAndBonuses()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":5,"class_options":{"fighting_style":"Archery"},"weapon_masteries":["Longbow"]}""")!.AsObject();
        var result=Rules().Derive(raw,Build("Fighter"),Features("Martial Weapons"),new(),[
            new(){Id="bow",Name="Briarbloom Bow",BaseWeapon="Longbow",Equipped=true},
            new(){Id="sword",Name="Dragon Slayer Shortsword",BaseWeapon="Shortsword",AttackBonus=1,DamageBonus=1,Equipped=true},
            new(){Id="spare",Name="Oathbow",BaseWeapon="Longbow",AttackBonus=3,Equipped=false}]);
        var bow=result.Attacks.Single(a=>a.Name=="Briarbloom Bow");
        Assert.AreEqual("weapon:briarbloom-bow",bow.Id);Assert.AreEqual(9,bow.AttackBonus,"Dexterity 4, proficiency 3 and Archery 2 come from the Longbow it works as.");
        Assert.AreEqual(8,bow.DamageDice!.Sides);Assert.AreEqual(4,bow.DamageModifier);Assert.AreEqual("Piercing",bow.DamageType);StringAssert.Contains(bow.Notes,"Slow");
        var sword=result.Attacks.Single(a=>a.Name=="Dragon Slayer Shortsword");
        Assert.AreEqual(Ability.Dexterity,sword.Ability);Assert.IsTrue(sword.Proficient);Assert.AreEqual(1,sword.FlatBonus);
        Assert.AreEqual(8,sword.AttackBonus);Assert.AreEqual(5,sword.DamageModifier);Assert.AreEqual(6,sword.DamageDice!.Sides);StringAssert.Contains(sword.Notes,"+1 to attack rolls and +1 to damage");
        Assert.IsFalse(result.Attacks.Any(a=>a.Name=="Oathbow"),"An unequipped weapon has no attack.");
        Assert.IsFalse(result.Attacks.Any(a=>a.Name is "Longbow" or "Shortsword"));
    }
    [TestMethod] public void AnItemThatWorksAsAnUnknownWeaponIsReportedNotGuessed()
    {
        var result=Rules().Derive(new JsonObject{["class"]="Fighter",["level"]=5},Build("Fighter"),Features("Martial Weapons"),new(),[new(){Id="odd",Name="Thornwhip",BaseWeapon="Thorn Lash",Equipped=true}]);
        Assert.IsFalse(result.Attacks.Any(a=>a.Kind=="weapon"));
        Assert.Contains("Thornwhip works as Thorn Lash, which is not a weapon in the catalog; it has no attack.",result.Warnings);
    }
}
