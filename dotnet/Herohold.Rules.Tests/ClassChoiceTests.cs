using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class ClassChoiceTests
{
    private static readonly Lazy<ClassChoiceRules> Native=new(()=>
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        var path=Path.Combine(root!.FullName,"data/imported/5etools");string Read(string name)=>File.ReadAllText(Path.Combine(path,name+".json"));
        var raw=File.ReadAllText(Path.Combine(root.FullName,"data/raw/5etools/spells/spells-xphb.json"));
        return new(ClassProgressionCatalog.Parse(Read("classes")),RulesCatalogSnapshot.Parse(Read("classes"),Read("species"),Read("backgrounds"),Read("feats"),Read("spells"),Read("equipment"),Read("weapons"),raw),BeastChoiceCatalog.Parse(Read("beasts")),SpellCapabilityCatalog.Parse(raw));
    });
    [TestCategory("RulesData")][TestMethod] public void StartingOrdersStylesAndAllClassLevelThresholdsAreNative()
    {
        foreach(var name in new[]{"Barbarian","Bard","Cleric","Druid","Fighter","Monk","Paladin","Ranger","Rogue","Sorcerer","Warlock","Wizard"})
        {Assert.IsNotNull(Native.Value.Describe(new(){ClassName=name},1));Assert.IsNotNull(Native.Value.Describe(new(){ClassName=name,Level=20},20));}
        Assert.IsTrue(Native.Value.Describe(new(){ClassName="Fighter"},1).Definitions.Any(d=>d.Key=="fighting_style"));
        Assert.IsTrue(Native.Value.Describe(new(){ClassName="Cleric"},1).Definitions.Any(d=>d.Key=="divine_order"));
        Assert.IsTrue(Native.Value.Describe(new(){ClassName="Druid"},1).Definitions.Any(d=>d.Key=="primal_order"));
        Assert.IsFalse(Native.Value.OwnedKeys("Rogue").Contains("class_skills"));
    }
    [TestCategory("RulesData")][TestMethod] public void ClassLanguageChoicesPermitRareSecretLanguagesButNeverDuplicateUniversalCommon()
    {
        var ranger=Native.Value.Describe(new(){ClassName="Ranger",Level=2},2).Definitions.Single(choice=>choice.Key=="ranger_languages");
        Assert.IsTrue(ranger.ValidExistingOptions.Contains("Druidic"));Assert.IsTrue(ranger.ValidExistingOptions.Contains("Thieves' Cant"));Assert.IsFalse(ranger.ValidExistingOptions.Contains("Common"));
        var rogue=Native.Value.Describe(new(){ClassName="Rogue",Level=1,ProficiencySources=[new("class_feature:Thieves' Cant",ProficiencySourceKind.Language,"Thieves' Cant")]},1).Definitions.Single(choice=>choice.Key=="rogue_thieves_cant_language");
        Assert.IsTrue(rogue.ValidExistingOptions.Contains("Druidic"));Assert.IsFalse(rogue.ValidExistingOptions.Contains("Thieves' Cant"));
    }
    [TestMethod] public void WarriorCantripsCountAsTheReceivingClassNotTheirSelectionList()
    {
        foreach(var (name,style,key,spell) in new[]{("Paladin","Blessed Warrior","blessed_warrior_cantrips","Sacred Flame"),("Ranger","Druidic Warrior","druidic_warrior_cantrips","Guidance")})
        {
            var raw=new JsonObject{["class"]=name,["level"]=2,["class_options"]=new JsonObject{["fighting_style"]=style,[key]=new JsonArray(spell)}};
            var source=CanonicalFixtures.ChoiceContext(raw,new(){ClassName=name}).CantripSources.Single(s=>s.SpellName==spell);
            CollectionAssert.AreEqual(new[]{name},source.CountsAsClasses.ToArray());
        }
    }
    [TestCategory("RulesData")][TestMethod] public void PactTomeHasRequiredSourceAwareChildrenThatDisappearWithParent()
    {
        var input=new ClassChoiceContext{ClassName="Warlock",ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"eldritch_invocations",new JsonArray("Pact of the Tome")},{"pact_tome_cantrips",new JsonArray("Fire Bolt","Guidance","Shillelagh")},{"pact_tome_rituals",new JsonArray("Alarm","Detect Magic")}}),CantripSources=[new("Eldritch Blast","class:Warlock",["Warlock"]),new("Fire Bolt","class_options:pact_tome_cantrips",["Warlock"])],PreparedSpellSources=[new("Comprehend Languages","class:Warlock"),new("Alarm","class_options:pact_tome_rituals")]};
        var result=Native.Value.Describe(input,1);var cantrips=result.Definitions.Single(choice=>choice.Key=="pact_tome_cantrips");var rituals=result.Definitions.Single(choice=>choice.Key=="pact_tome_rituals");
        Assert.AreEqual(3,cantrips.Choose);Assert.AreEqual(BuildChoiceStatus.Resolved,cantrips.Status);Assert.IsFalse(cantrips.ValidExistingOptions.Contains("Eldritch Blast"));Assert.IsTrue(cantrips.ValidExistingOptions.Contains("Fire Bolt"));
        Assert.AreEqual(BuildChoiceStatus.Resolved,rituals.Status);Assert.IsFalse(rituals.ValidExistingOptions.Contains("Comprehend Languages"));Assert.IsTrue(rituals.ValidExistingOptions.Contains("Alarm"));
        var removed=input with{ClassOptions=input.ClassOptions.ToDictionary(pair=>pair.Key,pair=>pair.Key=="eldritch_invocations"?CanonicalFixtures.Choice(new JsonArray("Armor of Shadows")):pair.Value)};
        Assert.IsFalse(Native.Value.Describe(removed,1).Definitions.Any(choice=>choice.Key.StartsWith("pact_tome_")));
    }
    [TestCategory("RulesData")][TestMethod] public void DuplicateOverfullAndIneligibleSelectionsRemainUnresolvedAndRawSelectionIsPreserved()
    {
        var input=new ClassChoiceContext{ClassName="Rogue",ProficiencySources=[new("base",ProficiencySourceKind.Skill,"Insight"),new("base",ProficiencySourceKind.Skill,"Stealth"),new("base",ProficiencySourceKind.Skill,"Arcana")],ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"expertise",new JsonArray("Insight","Insight")}})};
        var duplicate=Native.Value.Describe(input,1).Definitions.Single(d=>d.Key=="expertise");
        Assert.AreEqual(1,duplicate.ValidSelected.Count);Assert.AreEqual(1,duplicate.Remaining);Assert.AreEqual(BuildChoiceStatus.Unresolved,duplicate.Status);Assert.IsTrue(duplicate.Issues.Count>0);
        duplicate.PreservedSelection!.Items!.Clear();Assert.AreEqual(2,input.ClassOptions["expertise"].Items!.Count);
        foreach(var selection in new[]{new JsonArray("Insight","Stealth","Arcana"),new JsonArray("Insight","Hacking")})
            Assert.AreEqual(BuildChoiceStatus.Unresolved,Native.Value.Describe(input with{ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"expertise",selection}})},1).Definitions.Single(d=>d.Key=="expertise").Status);
    }
    [TestCategory("RulesData")][TestMethod] public void ExistingOwnedChoicesAreValidatedSeparatelyFromNewOptions()
    {
        var context=new ClassChoiceContext{ClassName="Ranger",Level=2,ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"ranger_languages",new JsonArray("Elvish","Dwarvish")}}),ProficiencySources=[new("base",ProficiencySourceKind.Language,"Common"),new("class_options:ranger_languages",ProficiencySourceKind.Language,"Elvish"),new("class_options:ranger_languages",ProficiencySourceKind.Language,"Dwarvish")]};
        var choice=Native.Value.Describe(context,2).Definitions.Single(d=>d.Key=="ranger_languages");
        Assert.AreEqual(BuildChoiceStatus.Resolved,choice.Status);Assert.IsTrue(choice.ValidExistingOptions.Contains("Elvish"));Assert.IsFalse(choice.NewOptions.Contains("Elvish"));Assert.IsFalse(choice.ValidExistingOptions.Contains("Common"));
    }
    [TestCategory("RulesData")][TestMethod] public void ScholarRequiresOtherSourceProficiencyAndInactiveWarriorChoicesDoNotAppear()
    {
        var wizard=Native.Value.Describe(new(){ClassName="Wizard",Level=2,ProficiencySources=[new("base",ProficiencySourceKind.Skill,"Arcana"),new("class_options:wizard_scholar_skill",ProficiencySourceKind.Expertise,"Arcana")]},2);
        CollectionAssert.AreEqual(new[]{"Arcana"},wizard.Definitions.Single(d=>d.Key=="wizard_scholar_skill").ValidExistingOptions.ToArray());
        var paladin=new ClassChoiceContext{ClassName="Paladin",ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"fighting_style",JsonValue.Create("Blessed Warrior")}})};
        Assert.IsFalse(Native.Value.Describe(paladin,1).Definitions.Any(d=>d.Key=="blessed_warrior_cantrips"));
        Assert.IsTrue(Native.Value.Describe(paladin with{Level=2},2).Definitions.Any(d=>d.Key=="blessed_warrior_cantrips"));
    }
    [TestCategory("RulesData")][TestMethod] public void InvocationDamageEligibilityUsesActualWarlockSpellSourceIncludingPactTome()
    {
        var context=new ClassChoiceContext{ClassName="Warlock",Level=2,CantripSources=[new("Sacred Flame","cleric-grant",["Cleric"])]};
        Assert.IsFalse(Native.Value.Describe(context,2).Definitions.Single(d=>d.Key=="eldritch_invocations").ValidExistingOptions.Contains("Agonizing Blast"));
        var pact=context with{CantripSources=[new("Fire Bolt","pact-tome",["Warlock"])]};
        Assert.IsTrue(Native.Value.Describe(pact,2).Definitions.Single(d=>d.Key=="eldritch_invocations").ValidExistingOptions.Contains("Agonizing Blast"));
    }
    [TestCategory("RulesData")][TestMethod] public void SubclassAliasesAndSavantThresholdsUseTrustedCatalogIdentity()
    {
        var context=new ClassChoiceContext{ClassName="wizard",SubclassName="Abjurer",Level=5,Spellbook=["Shield"]};
        var report=Native.Value.Describe(context,5);
        Assert.AreEqual(2,report.Definitions.Single(d=>d.Key=="savant_spells").Choose);Assert.AreEqual(1,report.Definitions.Single(d=>d.Key=="savant_spells_level_5").Choose);
        Assert.IsFalse(report.Definitions.Single(d=>d.Key=="savant_spells").NewOptions.Contains("Shield"));
        Assert.IsFalse(Native.Value.Describe(context with{Level=2},2).Definitions.Any(d=>d.Key.StartsWith("savant")));
    }
    [TestCategory("RulesData")][TestMethod] public void ForLevelUpReturnsNewlyDueUnresolvedChoicesWithoutReplayingOlderFeatures()
    {
        var report=Native.Value.ForLevelUp(new(){ClassName="Bard",Level=8},8,9);
        Assert.IsTrue(report.Definitions.Any(d=>d.Key=="bard_level_9_expertise"));Assert.IsFalse(report.Definitions.Any(d=>d.Key=="bard_expertise"));
        Assert.Throws<ArgumentException>(()=>Native.Value.ForLevelUp(new(){ClassName="Bard"},1,3));
    }
    [TestCategory("RulesData")][TestMethod] public void LateWizardFeaturesOrderCantripsAndStudentOfWarAreOwnedAndSelectable()
    {
        var wizard=Native.Value.Describe(new(){ClassName="Wizard",Level=20,Spellbook=["Magic Missile","Misty Step","Scorching Ray","Fireball","Counterspell"]},20);
        CollectionAssert.AreEqual(new[]{"Magic Missile"},wizard.Definitions.Single(d=>d.Key=="spell_mastery_level_1").ValidExistingOptions.ToArray());
        CollectionAssert.AreEqual(new[]{"Scorching Ray"},wizard.Definitions.Single(d=>d.Key=="spell_mastery_level_2").ValidExistingOptions.ToArray());
        Assert.AreEqual(2,wizard.Definitions.Single(d=>d.Key=="signature_spells").Choose);
        var cleric=Native.Value.Describe(new(){ClassName="Cleric",ClassOptions=CanonicalFixtures.Choices(new Dictionary<string,JsonNode?>{{"divine_order",JsonValue.Create("Thaumaturge")}})},1);
        Assert.AreEqual(1,cleric.Definitions.Single(d=>d.Key=="extra_cantrips").Choose);
        var fighter=Native.Value.Describe(new(){ClassName="Fighter",SubclassName="Battle Master",Level=3},3);
        Assert.IsTrue(fighter.Definitions.Single(d=>d.Key=="student_of_war_skill").ValidExistingOptions.Contains("Athletics"));
        Assert.AreEqual(BuildChoiceMode.Multi,Native.Value.Describe(new(){ClassName="Fighter",SubclassName="Champion",Level=7},7).Definitions.Single(d=>d.Key=="champion_additional_fighting_styles").Mode);
    }
    [TestCategory("RulesData")][TestMethod] public void Reviewed2024StylePermissionsAndOtherSourceRangerExpertiseAreApplied()
    {
        foreach(var name in new[]{"Fighter","Paladin","Ranger"})
        {
            var styles=Native.Value.Describe(new(){ClassName=name,Level=2},2).Definitions.Single(d=>d.Key=="fighting_style").ValidExistingOptions;
            Assert.IsTrue(styles.Contains("Archery"));Assert.IsTrue(styles.Contains("Two-Weapon Fighting"));Assert.AreEqual(name=="Fighter"?10:11,styles.Count);
        }
        var context=new ClassChoiceContext{ClassName="Ranger",Level=2,ProficiencySources=[new("base",ProficiencySourceKind.Skill,"Arcana"),new("feat:skill-expert",ProficiencySourceKind.Expertise,"Arcana"),new("base",ProficiencySourceKind.Skill,"Insight"),new("class_options:ranger_deft_explorer_expertise",ProficiencySourceKind.Expertise,"Insight")]};
        var definition=Native.Value.Describe(context,2).Definitions.Single(d=>d.Key=="ranger_deft_explorer_expertise");
        Assert.IsFalse(definition.ValidExistingOptions.Contains("Arcana"));Assert.IsTrue(definition.ValidExistingOptions.Contains("Insight"));
    }
    [TestMethod] public void NamedInvocationFallbackRetainsReviewedPrerequisitesWithoutDescriptionParsing()
    {
        var classes=ClassProgressionCatalog.Parse("""
            [{"name":"Warlock","source":"XPHB","hit_die":8,"level_1_rules":{"eldritch_invocations":1},"class_options":{"eldritch_invocations":{"from":["Agonizing Blast","Armor of Shadows","Thirsting Blade"]}}}]
            """);
        var empty="[]";var raw="{\"spell\":[]}";
        var rules=new ClassChoiceRules(classes,RulesCatalogSnapshot.Parse(empty,empty,empty,empty,empty,empty,empty,raw),BeastChoiceCatalog.Parse(empty),SpellCapabilityCatalog.Parse(raw));
        CollectionAssert.AreEqual(new[]{"Armor of Shadows"},rules.Describe(new(){ClassName="Warlock"},1).Definitions.Single(d=>d.Key=="eldritch_invocations").ValidExistingOptions.ToArray());
    }
}
