using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
namespace Herohold.TestSupport;

/// <summary>
/// Test conveniences over the rules builder: a rules character document in, the calculated document out. Production
/// reads documents only at the import edge (<see cref="NativeCharacterBuilder.Import(JsonObject)"/>).
/// </summary>
internal static class BuilderDocuments
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    private static string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var text)?text:"";
    private static IEnumerable<string> Strings(JsonNode? n)=>n is JsonArray array?array.Select(Text).Where(s=>s.Length>0):[];
    private static JsonArray Array(IEnumerable<string> names)=>new(names.Distinct(StringComparer.Ordinal).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
    private static JsonNode? Encode<T>(T value)=>JsonSerializer.SerializeToNode(value,Json);
    /// <summary>An uploaded rules character document with an owned feat's choices repaired.</summary>
    public static JsonObject RepairFeat(this NativeCharacterBuilder builder,JsonObject character,string instanceId,ChoiceValue value)
        =>builder.RepairFeat(BuildFacts.FromCanonical(character),builder.FeatContext(character),instanceId,value).ReplaceIn(character,Text(character["character_id"]));

    /// <summary>An uploaded rules character document, calculated, in the rules' document shape: its character, choices and validation.</summary>
    public static JsonObject ResultFor(this NativeCharacterBuilder builder,JsonObject character)
    {
        var calculation=builder.Calculate(builder.Import(character),null,false);
        return new()
        {
            ["character"]=Document(calculation),
            ["choices"]=new JsonArray([..calculation.Choices.Select(d=>(JsonNode?)NativeChoiceProjection.ToJson(d))]),
            ["validation"]=new JsonObject{["complete"]=calculation.Validation.Complete,["missing"]=Array(calculation.Validation.Missing),["unsupported"]=new JsonArray(),["warnings"]=Array(calculation.Validation.Warnings)},
            ["playMetadata"]=new JsonObject{["attacks"]=Encode(calculation.Attacks),["resources"]=Encode(calculation.Resources),["attackActionCount"]=calculation.AttackActionCount},
            ["sheet"]=new JsonObject{["feature_details"]=Encode(calculation.FeatureDetails),["spells"]=new JsonObject{["granted"]=Array(calculation.GrantedSpellNames),["spellbook"]=Array(calculation.Spellbook)}}
        };
    }
    /// <summary>An uploaded rules character document as the rules calculate it.</summary>
    public static JsonObject Recalculate(this NativeCharacterBuilder builder,JsonObject character)=>builder.ResultFor(character)["character"]!.AsObject();
    /// <summary>The builder choices of an uploaded rules character document, reading back what it showed.</summary>
    public static CreationChoiceReport Describe(this NativeCharacterBuilder builder,JsonObject character)=>builder.Describe(Shown(character),character["folio_inventory"] is not null,Found(character));
    public static FeatRuleContext FeatContext(this NativeCharacterBuilder builder,JsonObject character)=>builder.FeatContext(Shown(character),character["folio_inventory"] is not null,Found(character));
    private static DraftCharacter Shown(JsonObject character)
    {
        var equipment=(character["folio_inventory"]??character["folio_derived_inventory"])?.Deserialize<List<EquipmentItem>>(Json)??[];
        DraftScope? scope=character["folio_total_level"] is JsonValue total&&total.TryGetValue<int>(out var level)?new(level,character["folio_additional_class"]?.GetValue<bool>()==true):null;
        return new(BuildFacts.FromCanonical(character),equipment,scope);
    }
    private static FoundValues Found(JsonObject character)=>new(Strings(character["features"]).ToArray(),Strings(character["expertise"]).ToArray(),SpellGrantRecord.FromDocument(character["spell_grant_details"]));

    /// <summary>A calculation in the rules' character document shape, for fixtures and assertions on sheet values.</summary>
    public static JsonObject Document(CharacterCalculation c)
    {
        var doc=c.Build.ToCanonical(c.Id);
        doc[c.OwnsInventory?"folio_inventory":"folio_derived_inventory"]=Encode(c.Equipment);
        doc["include_default_equipment"]=!c.OwnsInventory&&c.Build.EquipmentMigrationPending!=true;doc["custom_equipment"]=Array(c.CustomEquipment);
        doc["equipment"]=Array(c.Equipment.Where(i=>i.Equipped&&i.Quantity>0).Select(i=>i.Name));
        if(c.Notes.Length>0)doc["notes"]=c.Notes;
        doc["hit_points"]=new JsonObject{["maximum"]=c.MaximumHp,["max"]=c.MaximumHp,["current"]=c.CurrentHp,["temporary"]=c.TemporaryHp,["hit_die"]="d"+c.HitDie};
        if(!c.KnownClass)return doc;
        doc["ability_scores"]=Encode(c.Scores);doc["ability_modifiers"]=Encode(c.Modifiers);doc["saving_throw_bonuses"]=Encode(c.SaveBonuses);doc["skill_bonuses"]=Encode(c.SkillBonuses);
        doc["ability_score_adjustment_bonuses"]=Encode(c.AdjustmentBonuses);doc["initiative"]=c.Initiative;doc["proficiency_bonus"]=CharacterCalculations.ProficiencyBonus(c.Build.Level);
        doc["saving_throw_proficiencies"]=Encode(c.SaveProficiencies.Select(a=>a.ToString()));doc["skill_proficiencies"]=Encode(c.SkillProficiencies);doc["expertise"]=Encode(c.Expertise);
        doc["languages"]=Encode(c.Languages);foreach(var pair in c.Facts)doc[pair.Key]=Encode(pair.Value);
        doc["features"]=Array(c.Features);doc["species_traits"]=Array(c.SpeciesTraits);doc["origin_feats"]=Array(c.OriginFeats);doc["size"]=c.Size;doc["senses"]=Encode(c.Senses);
        doc["armor_class"]=c.ArmorClass;doc["armor_class_details"]=c.Armor is not {} armor?null:new JsonObject{["value"]=armor.Value,["formula_id"]=armor.FormulaId,["source"]=armor.Source,["source_name"]=armor.SourceName,["shield_applied"]=armor.ShieldApplied,["defense_bonus"]=armor.DefenseBonus,["medium_armor_dexterity_cap"]=armor.MediumArmorDexterityCap,
            ["candidates"]=new JsonArray([..armor.Candidates.Select(a=>(JsonNode?)new JsonObject{["id"]=a.Id,["name"]=a.Name,["source"]=a.Source,["base_value"]=a.BaseValue,["value"]=a.Value,["allows_shield"]=a.AllowsShield,["shield_applied"]=a.ShieldApplied,["defense_bonus"]=a.DefenseBonus})])};doc["speed"]=c.Speed;
        doc["speed_details"]=new JsonObject{["base"]=c.BaseSpeed,["value"]=c.Speed,["modifiers"]=new JsonArray([..c.SpeedTerms.Select(t=>(JsonNode?)new JsonObject{["name"]=t.Name,["amount"]=t.Amount,["applies"]=t.Applies})])};
        doc["derived_resources"]=Encode(c.DerivedResources);doc["folio_native_standing_terms"]=Encode(c.StandingTerms);
        doc["spellcasting_sources"]=new JsonArray([..c.CastingSources.Select(s=>(JsonNode?)new JsonObject{["source_name"]=s.SourceName,["source_type"]=s.SourceType,["ability"]=s.Ability.ToString(),["spell_save_dc"]=s.SpellSaveDc,["spell_attack_bonus"]=s.SpellAttackBonus})]);
        var manual=c.Build.ManualFacts;
        doc["folio_build_fact_sources"]=new JsonObject{["manual"]=Encode(manual??new()),["derived"]=Encode(c.DerivedFacts)};
        doc["folio_language_sources"]=new JsonObject{["manual"]=Encode(c.Build.ManualLanguages??[]),["derived"]=Encode(c.Languages.Except(c.Build.ManualLanguages??[]))};
        var skills=c.Build.ManualSkills??[];var expertise=c.Build.ManualExpertise??[];var saves=c.Build.ManualSaves??[];
        doc["folio_native_build_sources"]=new JsonObject{["skills"]=Encode(skills),["expertise"]=Encode(expertise),["saves"]=Encode(saves),
            ["derivedSkills"]=Encode(c.SkillProficiencies.Except(skills)),["derivedExpertise"]=Encode(c.Expertise.Except(expertise)),["derivedSaves"]=Encode(c.SaveProficiencies.Select(a=>a.ToString()).Except(saves))};
        return doc;
    }
}
