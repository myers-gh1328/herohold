using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass] public sealed class SpellGrantRulesTests
{
    private static SpellGrantRules Rules()=>new(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Snapshot(),SpellGrantFixture.Species());
    private static Dictionary<Ability,int> Modifiers()=>Enum.GetValues<Ability>().ToDictionary(ability=>ability,ability=>ability==Ability.Wisdom?3:ability==Ability.Charisma?4:ability==Ability.Intelligence?2:0);
    [TestMethod] public void ShadowArtsUsesWisdomAndExistingFocusRatherThanAnUnresolvedCasterOrFreePool()
    {
        var raw=new JsonObject{["class"]="Monk",["subclass"]="Warrior of Shadow",["level"]=3};
        var result=Rules().Derive(raw,Modifiers());
        Assert.AreEqual(Ability.Wisdom,result.Metadata.Single(g=>g.SpellName=="Minor Illusion").Ability);
        var darkness=result.Metadata.Single(g=>g.SpellName=="Darkness");Assert.AreEqual(Ability.Wisdom,darkness.Ability);
        Assert.AreEqual("focus",darkness.ResourceId);Assert.AreEqual(0,darkness.MaxUses);Assert.IsFalse(darkness.AtWill);Assert.IsFalse(darkness.AllowsSlots);
        Assert.IsFalse(result.Warnings.Any(w=>w.Contains("choose its spellcasting ability",StringComparison.Ordinal)));
        raw["level"]=2;Assert.IsFalse(Rules().Derive(raw,Modifiers()).Metadata.Any(g=>g.SpellName is "Minor Illusion" or "Darkness"));
        raw["level"]=3;raw["subclass"]="Warrior of the Open Hand";Assert.IsFalse(Rules().Derive(raw,Modifiers()).Metadata.Any(g=>g.SpellName=="Darkness"));
    }
    [TestMethod] public void UnresolvedCastingAbilityCannotAuthorizeAnOrdinaryFallbackSpell()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","species":"Human","background":"Soldier","level":5,"general_feats":[{"name":"Fey-Touched","feat_id":"fey_touched_xphb","instance_id":"fictional-feat","choices":{"spell":"Charm Person"}}]}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        Assert.AreEqual(0,result.Metadata.Count);Assert.IsFalse(result.GrantedSpellNames.Contains("Misty Step"));Assert.IsTrue(result.Records.Count>0);Assert.IsTrue(result.Warnings.Count>0);
    }
    [TestMethod] public void PactTomeSelectedSpellsAreWarlockGrantsAndRemovalDeactivatesThem()
    {
        var raw=JsonNode.Parse("""{"class":"Warlock","species":"Human","background":"Soldier","level":2,"spells":{"cantrips":["Eldritch Blast"],"prepared":["Comprehend Languages"]},"class_options":{"eldritch_invocations":["Pact of the Tome"],"pact_tome_cantrips":["Fire Bolt","Guidance","Shillelagh"],"pact_tome_rituals":["Alarm","Detect Magic","Comprehend Languages"]}}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        Assert.AreEqual(5,result.Metadata.Count(grant=>grant.Source=="Pact of the Tome"));Assert.AreEqual(Ability.Charisma,result.Metadata.Single(grant=>grant.SpellName=="Guidance").Ability);
        var alarm=result.Metadata.Single(grant=>grant.SpellName=="Alarm");Assert.IsFalse(alarm.RitualOnly);Assert.IsTrue(alarm.AllowsSlots);Assert.IsNull(alarm.ResourceId);
        Assert.IsFalse(result.Records.Any(record=>record.Name=="Comprehend Languages"));
        raw["class_options"]!["eldritch_invocations"]=new JsonArray("Armor of Shadows");result=Rules().Derive(raw,Modifiers());Assert.IsFalse(result.Metadata.Any(grant=>grant.Source=="Pact of the Tome"));
    }
    [TestMethod] public void FixedInvocationSpellsHaveTheirOwnCastingAndCostPolicies()
    {
        var raw=JsonNode.Parse("""{"class":"Warlock","species":"Human","background":"Soldier","level":5,"class_options":{"eldritch_invocations":["Armor of Shadows","Otherworldly Leap","Gift of the Depths","Pact of the Chain","Fiendish Vigor"]}}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        foreach(var name in new[]{"Mage Armor","Jump","Find Familiar","False Life"})
        {var grant=result.Metadata.Single(grant=>grant.SpellName==name);Assert.IsTrue(grant.AtWill);Assert.AreEqual(Ability.Charisma,grant.Ability);Assert.IsNull(grant.ResourceId);}
        var breathing=result.Metadata.Single(grant=>grant.SpellName=="Water Breathing");Assert.AreEqual(1,breathing.MaxUses);Assert.AreEqual("long_rest",breathing.Recovery);Assert.IsFalse(breathing.AtWill);
        raw["level"]=1;result=Rules().Derive(raw,Modifiers());Assert.IsTrue(result.Metadata.Any(grant=>grant.SpellName=="Mage Armor"));Assert.IsFalse(result.Metadata.Any(grant=>grant.SpellName=="Jump"||grant.SpellName=="Water Breathing"||grant.SpellName=="False Life"));
    }
    [TestMethod] public void LoreMagicalDiscoveriesIncludesEligibleCantripsAndKeepsCastingSource()
    {
        var raw=JsonNode.Parse("""{"class":"Bard","subclass":"College of Lore","species":"Human","background":"Soldier","level":6,"class_options":{"magical_discoveries":["Fire Bolt","Counterspell"]}}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        Assert.IsTrue(result.GrantedSpellNames.Contains("Fire Bolt"));
        Assert.AreEqual(Ability.Charisma,result.Metadata.Single(grant=>grant.SpellName=="Fire Bolt").Ability);
        Assert.IsTrue(result.Metadata.Single(grant=>grant.SpellName=="Fire Bolt").AtWill);
    }
    [TestMethod] public void GeneralFeatFreeCastSourcesKeepExactExistingIdentityAndAbility()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","species":"Human","background":"Soldier","level":5,"general_feats":[{"name":"Fey-Touched","feat_id":"fey_touched_xphb","instance_id":"fictional-feat","choices":{"ability":"Wisdom","spell":"Charm Person"}}]}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        var misty=result.Metadata.Single(grant=>grant.SpellName=="Misty Step");Assert.AreEqual("grant-31566731f99c94f74d6f9221",misty.Id);Assert.AreEqual(Ability.Wisdom,misty.Ability);Assert.AreEqual(1,misty.MaxUses);Assert.IsTrue(misty.AllowsSlots);
        Assert.IsTrue(result.Records.Any(record=>record.Name=="Charm Person"));Assert.IsTrue(result.Sources.Any(source=>source.SourceId=="feat:fictional-feat"));
    }
    [TestMethod] public void MagicInitiateUsesActualOriginSourceAndIndependentChosenCastingAbility()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","species":"Human","background":"Acolyte","level":1,"origin_feat_choices":{"cantrips":["Sacred Flame","Guidance"],"spell":"Bless","spellcasting_ability":"Charisma"}}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());var bless=result.Metadata.Single(grant=>grant.SpellName=="Bless");
        Assert.AreEqual(Ability.Charisma,bless.Ability);Assert.AreEqual(1,bless.MaxUses);Assert.AreEqual("long_rest",bless.Recovery);
        Assert.IsTrue(result.Metadata.Single(grant=>grant.SpellName=="Sacred Flame").AtWill);
    }
    [TestMethod] public void ArcanumUsesSeparateLevelSpecificRecordsAndNeverPactOrOrdinarySlots()
    {
        var raw=JsonNode.Parse("""{"class":"Warlock","species":"Human","background":"Soldier","level":17,"class_options":{"mystic_arcanum_6":["Eyebite"],"mystic_arcanum_7":["Forcecage"],"mystic_arcanum_8":["Power Word Stun"],"mystic_arcanum_9":["Foresight"]}}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());
        var records=result.Records.Where(record=>record.GrantType=="mystic_arcanum").ToList();Assert.AreEqual(4,records.Count);CollectionAssert.AreEqual(new int?[]{11,13,15,17},records.Select(record=>record.Source.Level).ToArray());
        foreach(var name in new[]{"Eyebite","Forcecage","Power Word Stun","Foresight"}){var grant=result.Metadata.Single(grant=>grant.SpellName==name);Assert.AreEqual(1,grant.MaxUses);Assert.IsFalse(grant.AllowsSlots);}
    }
    [TestMethod] public void ActiveSubclassAndFeatureGrantsUseThresholdsAndCurrentWisdomCapacity()
    {
        var raw=JsonNode.Parse("""{"class":"Ranger","subclass":"Fey Wanderer","species":"Elf","background":"Wayfarer","level":15}""")!.AsObject();
        var result=Rules().Derive(raw,Modifiers());Assert.AreEqual(3,result.Metadata.Single(grant=>grant.SpellName=="Misty Step"&&grant.Source=="Misty Wanderer").MaxUses);
        Assert.AreEqual("hunters",result.Metadata.Single(grant=>grant.SpellName=="Hunter's Mark").ResourceId);
        raw["level"]=14;Assert.IsFalse(Rules().Derive(raw,Modifiers()).Metadata.Any(grant=>grant.Source=="Misty Wanderer"));
    }
    [TestMethod] public void InactiveWarriorParentsAndInvalidMasteryChoicesCannotCreateFreeCasting()
    {
        var raw=JsonNode.Parse("""{"class":"Ranger","species":"Human","background":"Soldier","level":5,"class_options":{"fighting_style":"Archery","druidic_warrior_cantrips":["Shillelagh","Guidance"]}}""")!.AsObject();
        Assert.IsFalse(Rules().Derive(raw,Modifiers()).Metadata.Any(grant=>grant.Source=="Druidic Warrior"));
        raw["class"]="Wizard";raw["level"]=18;raw["spells"]=new JsonObject{["spellbook"]=new JsonArray("Misty Step")};raw["class_options"]=new JsonObject{["spell_mastery_level_2"]="Misty Step"};
        var result=Rules().Derive(raw,Modifiers());Assert.IsFalse(result.Metadata.Any(grant=>grant.Source=="Spell Mastery"));Assert.IsTrue(result.Warnings.Count>0);
    }
}
