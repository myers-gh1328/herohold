using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Microsoft.Extensions.DependencyInjection;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativePipelineTests
{
    private static string Root()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        return root!.FullName;
    }
    private static JsonObject Character(string name,string cls,int level)=>JsonNode.Parse("""
        {"name":"Fixture","class":"Bard","species":"Human","background":"Soldier","level":2,"base_ability_scores":{"Strength":10,"Dexterity":10,"Constitution":10,"Intelligence":10,"Wisdom":10,"Charisma":10},"species_options":{"skill_proficiencies":["Perception"],"origin_feats":["Tough"]},"background_options":{"primary_ability":"Strength","secondary_ability":"Constitution","tool_proficiencies":["Any gaming set"]},"class_options":{"class_skills":["Performance","Insight","Persuasion"]},"include_default_equipment":false,"custom_equipment":[]}
        """)!.AsObject().ApplyIdentity(name,cls,level);

    [TestMethod]
    public void DetachedUnresolvedExpertiseCanBeReviewedWhileAdoptionStaysStrict()
    {
        var raw=SampleCanonical.Create();raw["expertise"]=new JsonArray("Insight");raw["skill_proficiencies"]=new JsonArray("Stealth");
        var draft=TestStores.Builder.Import(raw);
        CollectionAssert.Contains(draft.Build.ManualExpertise,"Insight","Imported Expertise the build does not explain is kept for review.");
        var calculated=TestStores.Builder.Calculate(draft,null,false);
        Assert.IsTrue(calculated.SkillProficiencies.Contains("Insight"),"Kept Expertise brings its skill proficiency.");
        Assert.Throws<ArgumentException>(()=>WorkspaceProjection.FromCalculation(calculated with{Expertise=["Insight"],SkillProficiencies=["Stealth"]}),"Expertise without its skill is never adopted.");
    }

    [TestMethod]
    public void LivePipelineAddsNamedJackBonusWithoutChangingSavesOrInitiative()
    {
        var character=TestStores.Calculated(Character("Native Bard","Bard",2));
        var arcana=CharacterCalculations.Skill(character,"Arcana",Ability.Intelligence);
        Assert.AreEqual(1,arcana.Total);
        Assert.IsTrue(arcana.Terms.Any(t=>t.Label.StartsWith("Jack of All Trades")));
        Assert.AreEqual(0,CharacterCalculations.Initiative(character).Total);
        Assert.AreEqual(0,CharacterCalculations.Save(character,Ability.Wisdom).Total);
    }

    [TestMethod]
    public void NativeWorkflowDeniesMonkShieldArmorClass()
    {
        var raw=Character("Native Monk","Monk",3);
        raw["base_ability_scores"]!["Dexterity"]=16;raw["base_ability_scores"]!["Wisdom"]=18;
        raw["class_options"]!["class_skills"]=new JsonArray("Acrobatics","Stealth");
        raw["folio_inventory"]=JsonNode.Parse("""[{"id":"shield","name":"Shield","quantity":1,"equipped":true}]""");
        var result=TestStores.Builder.ResultFor(raw);
        Assert.AreEqual(15,result["character"]!["armor_class"]!.GetValue<int>(),"A Monk with a shield loses Unarmored Defense.");
        var corrected=TestStores.Calculated(raw);
        Assert.AreEqual(15,corrected.ArmorClass);
    }

    [TestMethod]
    public async Task ActualPortableExportKeepsNativeSkillBonusAfterLegacyRecalculation()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-native-pipeline-"+Guid.NewGuid().ToString("N"));
        try
        {
            var character=TestStores.Calculated(Character("Portable Bard","Bard",2));
            var repository=TestStores.Open(directory);await repository.SaveAsync(new(){Character=character},0);
            var session=new WorkspaceSession(repository,TestStores.Workflow);await session.InitializeAsync();await session.SwitchCharacterAsync(character.Id);
            var artifact=await session.ExportAsync("json");Assert.IsNull(session.Error);Assert.IsNotNull(artifact);
            var exported=JsonNode.Parse(artifact.Bytes)!;
            Assert.AreEqual(1,exported["skill_bonuses"]!["Arcana"]!.GetValue<int>());
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

internal static class NativeFixtureIdentity
{
    internal static JsonObject ApplyIdentity(this JsonObject raw,string name,string cls,int level)
    {raw["name"]=name;raw["class"]=cls;raw["level"]=level;return raw;}
}
