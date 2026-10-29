using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class NativeCharacterBuilderTests
{
    internal static NativeCharacterBuilder Builder()=>Herohold.TestSupport.TestStores.Builder;
    internal static FeatSelectionRules Feats()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/raw/5etools/feats.json")))root=root.Parent;
        return new(NativeCatalogIntegrationTests.Snapshot(),NativeSelectableFeatCatalog.Parse(File.ReadAllText(Path.Combine(root!.FullName,"data/raw/5etools/feats.json"))).AdditionalDefinitions,ToolCatalog.Parse(File.ReadAllText(Path.Combine(root.FullName,"data/raw/5etools/items-base.json")),File.ReadAllText(Path.Combine(root.FullName,"data/raw/5etools/items.json"))));
    }
    internal static JsonObject Fighter()
    {
        var c=CanonicalCharacterRules.Create();c["name"]="Native Test Fighter";c["class"]="Fighter";c["species"]="Orc";c["background"]="Soldier";c["origin_languages"]=new JsonArray("Elvish","Dwarvish");
        c["class_options"]=new JsonObject{["class_skills"]=new JsonArray("Perception","Survival"),["fighting_style"]="Defense"};
        c["background_options"]=new JsonObject{["primary_ability"]="Strength",["secondary_ability"]="Dexterity",["tool_proficiencies"]=new JsonArray("Dice Set")};c["weapon_masteries"]=new JsonArray("Longsword","Shortbow","Spear");return c;
    }
    [TestMethod] public void CompleteFighterCalculatesAndProjectsWithoutAnyCompatibilityRuntime()
    {
        var source=Fighter();var before=source.ToJsonString();var result=Builder().ResultFor(source);
        Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());var character=TestStores.Character(result);
        Assert.AreEqual(12,character.AbilityScores[Ability.Strength]);Assert.AreEqual(10,character.MaximumHp);Assert.IsTrue(character.Features.Contains("Second Wind"));Assert.AreEqual(before,source.ToJsonString());
        Assert.IsTrue(result["playMetadata"]!["attacks"]!.AsArray().Count>0,"Equipped starting weapons must be available as native attack actions.");
    }
    [TestMethod] public void SpeciesDefensesReachTheActualPlayerStateAndRepeatedCalculationDoesNotCompound()
    {
        var builder=Builder();var source=Fighter();source["species"]="Aasimar";var result=builder.ResultFor(source);var character=TestStores.Character(result);
        CollectionAssert.AreEquivalent(new[]{"Necrotic","Radiant"},character.Defenses.Select(d=>d.Type).ToArray());
        var first=builder.Recalculate(source);Assert.IsTrue(JsonNode.DeepEquals(first,builder.Recalculate(first)));
    }
    [TestMethod] public void MissingIdentityRemainsAnEditableDraftWithNoImplicitClassOrFeat()
    {
        var builder=Builder();var result=builder.ResultFor(CanonicalCharacterRules.Create());Assert.IsFalse(result["validation"]!["complete"]!.GetValue<bool>());
        Assert.AreEqual("",result["character"]!["class"]!.GetValue<string>());Assert.IsTrue(result["choices"]!.AsArray().Count>=5);
    }
    [TestMethod] public void StructuredInventoryModifierIsAppliedOnceAcrossNativeHpAndStandingValues()
    {
        var builder=Builder();var source=Fighter();source["folio_inventory"]=new JsonArray(new JsonObject{["id"]="ring",["name"]="Synthetic Ring",["equipped"]=true,["quantity"]=1,["modifierBonuses"]=new JsonObject{["Strength"]=1,["Dexterity"]=1,["Constitution"]=1,["Intelligence"]=1,["Wisdom"]=1,["Charisma"]=1}});
        var result=builder.ResultFor(source);var character=TestStores.Character(result);Assert.AreEqual(11,character.MaximumHp);Assert.AreEqual(2,CharacterCalculations.Ability(character,Ability.Strength).Total);Assert.AreEqual(1,CharacterCalculations.Ability(character,Ability.Wisdom).Total);
        Assert.IsTrue(JsonNode.DeepEquals(builder.Recalculate(source),builder.Recalculate(builder.Recalculate(source))));
    }
    internal static JsonObject Wizard()
    {
        var c=Fighter();c["class"]="Wizard";c["background"]="Sage";c["class_options"]=new JsonObject{["class_skills"]=new JsonArray("Investigation","Religion")};c["background_options"]=new JsonObject{["primary_ability"]="Intelligence",["secondary_ability"]="Constitution"};c["weapon_masteries"]=new JsonArray();
        c["spells"]=new JsonObject{["cantrips"]=new JsonArray("Fire Bolt","Mage Hand","Prestidigitation"),["prepared"]=new JsonArray("Shield"),["known"]=new JsonArray(),["spellbook"]=new JsonArray("Shield","Magic Missile","Detect Magic","Mage Armor","Find Familiar","Alarm")};
        c["origin_feat_choices"]=new JsonObject{["cantrips"]=new JsonArray("Light","Minor Illusion"),["spell"]="Shield",["spellcasting_ability"]="Wisdom"};return c;
    }
    [TestMethod] public void WizardAndMagicInitiateKeepSeparateCastingAbilitiesAndPaymentSources()
    {
        var result=Builder().ResultFor(Wizard());Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());var character=TestStores.Character(result);
        Assert.AreEqual(Ability.Intelligence,character.Play.SpellcastingAbility);var shields=character.Play.Spells.Where(s=>s.Name=="Shield").ToArray();Assert.IsTrue(shields.Any(s=>s.CastingAbility==Ability.Wisdom));Assert.IsTrue(shields.Any(s=>s.CastingAbility is null));
        Assert.AreEqual(1,character.Resources.Single(r=>r.SourceKind=="grant"&&r.Name.StartsWith("Shield")).Maximum);Assert.IsTrue(character.Play.SpellbookIds.Count>=6);
    }
    [TestMethod] public void SavantGrantedSpellsReachTheTypedSpellbookWithoutBeingLostOrAddedTwice()
    {
        var source=Wizard();source["level"]=3;source["subclass"]="Evoker";source["class_options"]!["wizard_scholar_skill"]="Arcana";source["class_options"]!["savant_spells"]=new JsonArray("Thunderwave","Shatter");
        foreach(var name in new[]{"Burning Hands","Chromatic Orb","Invisibility","Mirror Image"})source["spells"]!["spellbook"]!.AsArray().Add(name);
        var builder=Builder();var result=builder.ResultFor(source);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());
        Assert.AreEqual(12,TestStores.Character(result).Play.SpellbookIds.Count);Assert.AreEqual(10,result["character"]!["spells"]!["spellbook"]!.AsArray().Count,"Base book choices stay separate from automatic Savant sources.");
    }
    [TestMethod] public void StartingPackageQuantitiesProduceUsableWeaponActionsAndTypedInventory()
    {
        var result=Builder().ResultFor(Fighter());var character=TestStores.Character(result);Assert.AreEqual(8,character.Equipment.Single(i=>i.Name=="Javelin").Quantity);
        Assert.IsTrue(result["playMetadata"]!["attacks"]!.AsArray().OfType<JsonObject>().Any(a=>a["name"]!.GetValue<string>()=="Javelin"));Assert.IsFalse(result["character"]!.AsObject().ContainsKey("folio_inventory"),"An unconfirmed creation draft must retain starting-package selection.");
    }
    [TestMethod] public void ReplacingAMagicInitiateSpellKeepsItsAlreadySpentFreeCast()
    {
        var builder=Builder();var original=TestStores.Calculated(Wizard());var pool=original.Resources.Single(r=>r.SourceKind=="grant"&&r.Name.StartsWith("Shield"));
        original=original with{Resources=original.Resources.Select(r=>r.Id==pool.Id?r with{Remaining=0}:r).ToList()};var changed=WorkspaceProjection.ToCanonical(original);changed["origin_feat_choices"]!["spell"]="Magic Missile";
        var projected=TestStores.Calculated(changed,original);var replacement=projected.Resources.Single(r=>r.SourceKind=="grant"&&r.Name.StartsWith("Magic Missile"));
        Assert.AreNotEqual(pool.Id,replacement.Id);Assert.AreEqual(0,replacement.Remaining,"A spell replacement is not a rest and must not restore its feature's use.");
    }
    [TestMethod] public void MulticlassStateIsFlaggedInsteadOfReceivingSingleClassProgression()
    {
        var source=Fighter();source["level"]=2;source["class_levels"]=new JsonObject{["Fighter"]=1,["Wizard"]=1};var result=Builder().ResultFor(source);
        Assert.IsFalse(result["validation"]!["complete"]!.GetValue<bool>());Assert.IsTrue(result["validation"]!["missing"]!.ToJsonString().Contains("source-owned"));
    }
    [TestMethod] public void BackgroundEvenSplitReachesThePlayerSheetAndHidesInactiveWeightedChoices()
    {
        var source=Fighter();source["background_options"]!["ability_bonus_mode"]="three";source["background_options"]!["primary_ability"]="Intelligence";source["background_options"]!["secondary_ability"]="Charisma";
        var result=Builder().ResultFor(source);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());
        foreach(var ability in new[]{"Strength","Dexterity","Constitution"})Assert.AreEqual(11,result["character"]!["ability_scores"]![ability]!.GetValue<int>());
        Assert.IsFalse(result["choices"]!.AsArray().OfType<JsonObject>().Any(d=>d["id"]!.GetValue<string>() is "primary_ability" or "secondary_ability"));
    }
    [TestMethod] public void ImportedManualBuildFactsSurviveWhileRemovedSpeciesFactsDoNotBecomeManual()
    {
        var builder=Builder();var raw=Fighter();raw["species"]="Aasimar";raw["tool_proficiencies"]=new JsonArray("Herbalism Kit");raw["damage_resistances"]=new JsonArray("Cold");
        var first=builder.Recalculate(raw);Assert.IsTrue(first["tool_proficiencies"]!.AsArray().Any(n=>n!.GetValue<string>()=="Herbalism Kit"));Assert.IsTrue(first["damage_resistances"]!.AsArray().Any(n=>n!.GetValue<string>()=="Cold"));Assert.IsTrue(first["damage_resistances"]!.AsArray().Any(n=>n!.GetValue<string>()=="Radiant"));
        first["species"]="Orc";var changed=builder.Recalculate(first);Assert.IsFalse(changed["damage_resistances"]!.AsArray().Any(n=>n!.GetValue<string>()=="Radiant"));Assert.IsTrue(changed["damage_resistances"]!.AsArray().Any(n=>n!.GetValue<string>()=="Cold"));Assert.IsTrue(changed["tool_proficiencies"]!.AsArray().Any(n=>n!.GetValue<string>()=="Herbalism Kit"));Assert.IsTrue(JsonNode.DeepEquals(changed,builder.Recalculate(changed)));
        var state=TestStores.Calculated(changed);var edited=WorkspaceRules.SetDefenses(state,state.Defenses.Where(d=>d.Type!="Cold").ToList());var removed=builder.Recalculate(WorkspaceProjection.ToCanonical(edited));Assert.IsFalse(removed["damage_resistances"]!.AsArray().Any(n=>n!.GetValue<string>()=="Cold"),"A deliberately removed manual defense must not reappear on the next edit.");
    }

    [TestMethod] public void KnownFeatIdentityNormalizationReachesHpAndManualWeaponProficiencyReachesAttackActions()
    {
        var builder=Builder();var fighter=Fighter();fighter["level"]=4;fighter["subclass"]="Champion";fighter["general_feats"]=new JsonArray(new JsonObject{["instance_id"]="tough-case",["name"]="tough",["feat_id"]="tough_xphb",["level_taken"]=4,["choices"]=new JsonObject()});var calculated=builder.Recalculate(fighter);Assert.AreEqual("Tough",calculated["general_feats"]![0]!["name"]!.GetValue<string>());Assert.AreEqual(36,calculated["hit_points"]!["maximum"]!.GetValue<int>());
        var wizard=Wizard();wizard["weapon_proficiencies"]=new JsonArray("Longbow");wizard["folio_inventory"]=new JsonArray(new JsonObject{["id"]="bow",["name"]="Longbow",["equipped"]=true,["quantity"]=1});var result=builder.ResultFor(wizard);var attack=result["playMetadata"]!["attacks"]!.AsArray().Single(a=>a!["name"]!.GetValue<string>()=="Longbow")!;Assert.IsTrue(attack["proficient"]!.GetValue<bool>());Assert.AreEqual(2,attack["attackBonus"]!.GetValue<int>());
    }

}
