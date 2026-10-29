using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using Herohold.Core;
namespace Herohold.Web.Services;

public static class NativeExportProjection
{
    internal static IReadOnlyList<string> SkillNames=>Skills.Keys.ToArray();
    private static readonly JsonSerializerOptions Json=new(){Converters={new JsonStringEnumConverter()}};
    private static readonly IReadOnlyDictionary<string,Ability> Skills=new Dictionary<string,Ability>{
        ["Acrobatics"]=Ability.Dexterity,["Animal Handling"]=Ability.Wisdom,["Arcana"]=Ability.Intelligence,["Athletics"]=Ability.Strength,
        ["Deception"]=Ability.Charisma,["History"]=Ability.Intelligence,["Insight"]=Ability.Wisdom,["Intimidation"]=Ability.Charisma,
        ["Investigation"]=Ability.Intelligence,["Medicine"]=Ability.Wisdom,["Nature"]=Ability.Intelligence,["Perception"]=Ability.Wisdom,
        ["Performance"]=Ability.Charisma,["Persuasion"]=Ability.Charisma,["Religion"]=Ability.Intelligence,["Sleight of Hand"]=Ability.Dexterity,
        ["Stealth"]=Ability.Dexterity,["Survival"]=Ability.Wisdom};
    public static JsonObject Create(CharacterState character)
    {
        ArgumentNullException.ThrowIfNull(character);WorkspaceRules.Validate(character);
        // Canonical standing numbers exclude transient Exhaustion; the live sheet/PDF applies it once.
        var unexhausted=character with{Exhaustion=0};
        JsonNode? Map(IReadOnlyDictionary<Ability,int> values)=>JsonSerializer.SerializeToNode(values,Json);
        var modifiers=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Ability(character,a).Total);
        var bonuses=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>Math.Clamp(character.AbilityScores[a]-(character.Build?.BaseAbilityScores?.GetValueOrDefault(a,character.AbilityScores[a])??character.AbilityScores[a]),0,30));
        var sourceBonuses=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>character.Derived.AbilityAdjustmentBonuses.GetValueOrDefault(a));
        var snapshot=new JsonObject{
            ["ability_scores"]=Map(character.AbilityScores),["ability_modifiers"]=Map(modifiers),
            ["ability_score_bonuses"]=Map(bonuses),["ability_score_adjustment_bonuses"]=Map(sourceBonuses),
            ["saving_throw_bonuses"]=Map(Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Save(unexhausted,a).Total)),
            ["skill_bonuses"]=JsonSerializer.SerializeToNode(Skills.ToDictionary(pair=>pair.Key,pair=>CharacterCalculations.Skill(unexhausted,pair.Key,pair.Value).Total),Json),
            ["saving_throw_proficiencies"]=JsonSerializer.SerializeToNode(character.SaveProficiencies,Json),
            ["skill_proficiencies"]=JsonSerializer.SerializeToNode(character.SkillProficiencies,Json),["expertise"]=JsonSerializer.SerializeToNode(character.Expertise,Json),
            ["initiative"]=CharacterCalculations.Initiative(unexhausted).Total,["armor_class"]=character.ArmorClass,["speed"]=character.Speed};
        // Detail blocks (armor class, speed, resources, casting sources) come from the export's own recalculation of the same build.
        return snapshot;
    }
}
