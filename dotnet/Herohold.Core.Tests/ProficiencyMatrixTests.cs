using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class ProficiencyMatrixTests
{
    private sealed record LevelReference(int Level,int Proficiency,int Expertise);
    private sealed record AbilityScenario(string Name,int Score,int Modifier,int ItemBonus,List<int> Levels);
    private sealed record SkillReference(string Name,string Ability);
    private sealed record ReferenceData(List<LevelReference> Levels,List<AbilityScenario> AbilityScenarios,
        List<string> TrainingStates,List<SkillReference> Skills);
    private static readonly Lazy<ReferenceData> Data=new(()=>
        JsonSerializer.Deserialize<ReferenceData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"proficiency_matrix.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ??throw new InvalidDataException("The proficiency reference dataset is missing."));

    // One parameterized method: 60 level/training cases plus 6 focused item-bonus cases.
    // Expected proficiency, Expertise and ability modifiers come from explicit reference tables.
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var data=Data.Value;
            if(!data.Levels.Select(r=>r.Level).SequenceEqual(Enumerable.Range(1,20)) ||
               !data.TrainingStates.SequenceEqual(new[]{"untrained","proficient","expertise"}) ||
               data.Skills.Count!=18 || data.Skills.Select(r=>r.Name).Distinct().Count()!=18)
                throw new InvalidDataException("The complete proficiency matrix dimensions must not shrink silently.");
            foreach(var scenario in data.AbilityScenarios)
            foreach(var levelNumber in scenario.Levels)
            foreach(var training in data.TrainingStates)
            {
                var level=data.Levels.Single(r=>r.Level==levelNumber);
                var trainingBonus=training switch{"expertise"=>level.Expertise,"proficient"=>level.Proficiency,_=>0};
                var ordinaryProficiency=training=="untrained"?0:level.Proficiency;
                yield return new object[]{ $"{scenario.Name}: level={level.Level}, {training}",
                    level.Level,scenario.Score,scenario.Modifier,level.Proficiency,scenario.ItemBonus,training,
                    scenario.Modifier+scenario.ItemBonus+trainingBonus,scenario.Modifier+scenario.ItemBonus+ordinaryProficiency };
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void ProficiencyAndExpertiseMatchReferenceData(string caseName,int level,int score,
        int expectedModifier,int expectedProficiency,int itemBonus,string training,
        int expectedSkillTotal,int expectedSaveAndAttackTotal)
    {
        var trained=training!="untrained";
        var skills=Data.Value.Skills.Select(r=>r.Name).ToList();
        var abilities=Enum.GetValues<Ability>();
        var character=new CharacterState
        {
            Id="reference-matrix",Name="Reference matrix",Level=level,
            AbilityScores=abilities.ToDictionary(a=>a,_=>10),
            SkillProficiencies=trained?[..skills]:[],
            Expertise=training=="expertise"?[..skills]:[],
            SaveProficiencies=trained?[..abilities]:[],
            Equipment=itemBonus==0?[]:[new(){Id="modifier-item",Name="Reference item",Quantity=1,Equipped=true,
                ModifierBonuses=abilities.ToDictionary(a=>a,_=>itemBonus)}]
        };
        CharacterState ForAbility(Ability selected)=>character with{AbilityScores=abilities.ToDictionary(a=>a,a=>a==selected?score:10)};
        Assert.AreEqual(expectedProficiency,CharacterCalculations.ProficiencyBonus(level),caseName);
        foreach(var ability in abilities)
        {
            var abilityCase=ForAbility(ability);
            Assert.AreEqual(expectedModifier+itemBonus,CharacterCalculations.Ability(abilityCase,ability).Total,$"{caseName}, {ability} modifier");
            Assert.AreEqual(expectedSaveAndAttackTotal,CharacterCalculations.Save(abilityCase,ability).Total,$"{caseName}, {ability} save: Expertise must not double saving throw proficiency");
            Assert.AreEqual(expectedSaveAndAttackTotal,CharacterCalculations.Attack(abilityCase,ability,trained,0).Total,$"{caseName}, {ability} attack: Expertise must not double weapon proficiency");
            Assert.AreEqual(score,abilityCase.AbilityScores[ability],$"{caseName}, item modifiers must not change scores");
        }
        foreach(var skill in Data.Value.Skills)
        {
            var ability=Enum.Parse<Ability>(skill.Ability);
            var calculation=CharacterCalculations.Skill(ForAbility(ability),skill.Name,ability);
            Assert.AreEqual(expectedSkillTotal,calculation.Total,$"{caseName}, {skill.Name}: ability/item modifier plus the correct training bonus exactly once");
            Assert.AreEqual(expectedSkillTotal,calculation.Terms.Sum(t=>t.Value),$"{caseName}, {skill.Name} displayed breakdown");
        }
    }
}
