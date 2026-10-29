using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestClass]
public sealed class NativeExportTests
{
    [TestMethod]
    public void ExportSnapshotUsesFinalDomainCalculationsAndDetachedData()
    {
        var sample=SampleWorkspace.Create().Character;
        var character=sample with{ArmorClass=23,Speed=50,StandingAdjustments=new(){{"skill:Arcana",2}},StandingAdjustmentTerms=new(){{"skill:Arcana",[new("Reviewed skill feature",2)]}}};
        var snapshot=NativeExportProjection.Create(character);
        Assert.AreEqual(23,snapshot["armor_class"]!.GetValue<int>());
        Assert.AreEqual(50,snapshot["speed"]!.GetValue<int>());
        Assert.AreEqual(CharacterCalculations.Skill(character,"Arcana",Ability.Intelligence).Total,snapshot["skill_bonuses"]!["Arcana"]!.GetValue<int>());
        Assert.AreEqual(CharacterCalculations.Ability(character,Ability.Wisdom).Total,snapshot["ability_modifiers"]!["Wisdom"]!.GetValue<int>());
        snapshot["ability_scores"]!["Wisdom"]=1;
        Assert.AreEqual(sample.AbilityScores[Ability.Wisdom],character.AbilityScores[Ability.Wisdom]);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void NativeStandingTermsSurviveProjectionAndPortableCanonicalRoundTrip()
    {
        var raw=SampleCanonical.Create();raw["class"]="Bard";raw["subclass"]="";raw["hp_history"]=new JsonArray();
        var character=TestStores.Calculated(raw);
        var calculation=CharacterCalculations.Skill(character,"Arcana",Ability.Intelligence);
        var jack=calculation.Terms.Single(t=>t.Label.StartsWith("Jack of All Trades"));
        Assert.AreEqual(CharacterCalculations.ProficiencyBonus(character.Level)/2,jack.Value);
        var portable=WorkspaceProjection.ToCanonical(character);
        Assert.AreEqual(jack.Label,portable["folio_native_standing_terms"]!["skill:Arcana"]![0]!["label"]!.GetValue<string>());
        Assert.AreEqual(calculation.Total,CharacterCalculations.Skill(TestStores.Restored(portable),"Arcana",Ability.Intelligence).Total,"The named term survives a round trip.");
    }
}
