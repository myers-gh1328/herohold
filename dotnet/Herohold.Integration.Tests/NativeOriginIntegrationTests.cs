using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class NativeOriginIntegrationTests
{
    [TestMethod]public void SwitchingFromChampionDeactivatesRetainedExtraStyleArmorAndAttackBonuses()
    {
        var raw=NativeCharacterBuilderTests.Fighter();raw["level"]=7;raw["subclass"]="Champion";raw["class_options"]!["fighting_style"]="Dueling";raw["class_options"]!["champion_additional_fighting_styles"]=new JsonArray("Defense","Archery");
        raw["folio_inventory"]=new JsonArray(new JsonObject{["id"]="armor",["name"]="Chain Mail",["quantity"]=1,["equipped"]=true},new JsonObject{["id"]="bow",["name"]="Longbow",["quantity"]=1,["equipped"]=true});
        var builder=NativeCharacterBuilderTests.Builder();var champion=builder.ResultFor(raw);var beforeArmor=champion["character"]!["armor_class"]!.GetValue<int>();var beforeAttack=champion["playMetadata"]!["attacks"]!.AsArray().OfType<JsonObject>().Single(a=>a["name"]!.GetValue<string>()=="Longbow")["attackBonus"]!.GetValue<int>();
        raw["subclass"]="Battle Master";var changed=builder.ResultFor(raw);
        Assert.AreEqual(beforeArmor-1,changed["character"]!["armor_class"]!.GetValue<int>());Assert.AreEqual(beforeAttack-2,changed["playMetadata"]!["attacks"]!.AsArray().OfType<JsonObject>().Single(a=>a["name"]!.GetValue<string>()=="Longbow")["attackBonus"]!.GetValue<int>());
        Assert.AreEqual(2,changed["character"]!["class_options"]!["champion_additional_fighting_styles"]!.AsArray().Count);
    }
    [TestMethod] public void RemovingGeneralSkilledDropsOnlyItsOwnedSkillsAndPreservesImportedManualSkills()
    {
        var raw=NativeCharacterBuilderTests.Fighter();raw["skill_proficiencies"]=new JsonArray("Arcana");
        raw["general_feats"]=new JsonArray(new JsonObject{["instance_id"]="fictional-skilled",["name"]="Skilled",["choices"]=new JsonObject{["proficiencies"]=new JsonArray("Skill: Insight","Skill: Religion","Tool: Lute")}});
        var builder=Herohold.TestSupport.TestStores.Builder;var draft=builder.Import(raw);var first=builder.Calculate(draft,null,false);Assert.IsTrue(first.SkillProficiencies.Contains("Insight"));
        var skills=builder.Calculate(draft with{Build=first.Build with{Feats=[]}},null,false).SkillProficiencies;
        Assert.IsTrue(skills.Contains("Arcana"));Assert.IsFalse(skills.Contains("Insight"));Assert.IsFalse(skills.Contains("Religion"));
    }
    [TestMethod] public void GeneralDefenseStyleReachesNativeArmorAndDoesNotStackWithClassDefense()
    {
        var builder=NativeCharacterBuilderTests.Builder();var raw=NativeCharacterBuilderTests.Fighter();raw["class_options"]!["fighting_style"]="Archery";
        var baseline=builder.Recalculate(raw)["armor_class"]!.GetValue<int>();raw["general_feats"]=new JsonArray(new JsonObject{["instance_id"]="fictional-defense",["name"]="Defense"});
        var boosted=builder.Recalculate(raw);Assert.AreEqual(baseline+1,boosted["armor_class"]!.GetValue<int>());
        raw["class_options"]!["fighting_style"]="Defense";Assert.AreEqual(baseline+1,builder.Recalculate(raw)["armor_class"]!.GetValue<int>());
    }
    [TestMethod] public void ShadowDarknessCastsUsingOneExistingFocusWithoutCreatingAFreeCastPool()
    {
        var raw=NativeCharacterBuilderTests.Fighter();raw["class"]="Monk";raw["subclass"]="Warrior of Shadow";raw["level"]=3;raw["class_options"]=new JsonObject();raw["hit_points"]!["current"]=10;
        var character=Herohold.TestSupport.TestStores.Calculated(raw);var spell=character.Play.Spells.Single(s=>s.Name=="Darkness"&&s.GrantResourceId=="focus");
        Assert.AreEqual("focus",spell.GrantResourceId);Assert.IsTrue(spell.Concentration);Assert.AreEqual(1,character.Resources.Count(r=>r.Id=="focus"));
        var after=PlayRules.Cast(new(){Character=character,Revision=1},new(){SourceRevision=1,SourceCharacterId=character.Id,OperationId="fictional-shadow-cast",SpellId=spell.Id,SlotLevel=2,PaymentResourceId="focus",ConfirmConcentration=true,Mode=DiceMode.Physical});
        Assert.AreEqual(character.Resources.Single(r=>r.Id=="focus").Remaining-1,after.Resources.Single(r=>r.Id=="focus").Remaining);Assert.IsTrue(after.Effects.Any(e=>e.Concentration));
    }
}
