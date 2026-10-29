using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Web.Services;

public static partial class WorkspaceProjection
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
    /// <summary>Character keys that carry Herohold's own typed state; adopting a character moves them into typed fields.</summary>
    public static readonly IReadOnlyList<string> Sidecars=["folio_play","folio_inventory","folio_derived_inventory","folio_resources","folio_notes","folio_effects","folio_defenses","folio_known_target","folio_exhaustion","folio_native_standing_terms","folio_character_sheet","folio_combat"];
    /// <summary>The play facts an imported character document carries in its Herohold sidecars, or imports from its sheet fields.</summary>
    public static CharacterFacts ImportedFacts(JsonObject raw)
    {
        var hp=raw["hit_points"];var maximum=Math.Max(1,Number(hp?["maximum"],Number(hp?["max"],1)));var current=Math.Clamp(Number(hp?["current"],maximum),0,maximum);
        var play=Read<PlayState>(raw["folio_play"]);
        var notes=Read<List<JournalNote>>(raw["folio_notes"])??(string.IsNullOrWhiteSpace(Text(raw["notes"]))?[]:[new(){Id=Guid.NewGuid().ToString("N"),Title="Imported notes",Text=Text(raw["notes"])}]);
        return new(){CurrentHp=current,TemporaryHp=Number(hp?["temporary"]),Exhaustion=Number(raw["folio_exhaustion"]),Equipment=ResolveEquipment(raw,null),Notes=notes,
            Effects=Read<List<ActiveEffect>>(raw["folio_effects"])??[],Combat=Read<CombatState>(raw["folio_combat"])??new(),Sheet=Read<CharacterSheetState>(raw["folio_character_sheet"])??SheetRules.ImportState(raw,current),
            KnownTarget=Read<KnownTargetState>(raw["folio_known_target"])??new(),Defenses=[..(Read<List<DamageDefense>>(raw["folio_defenses"])??[]).Where(d=>d.Source!="Character build")],
            Resources=Read<List<ResourcePool>>(raw["folio_resources"])??[],HitDiePools=play?.HitDiePools??[],SpellNotes=play?.SpellNotes??new(StringComparer.Ordinal),DreadUsedThisTurn=play?.DreadUsedThisTurn??false,HunterTarget=play?.HunterTarget??""};
    }
    private static readonly Dictionary<string,Ability> SheetSkills=new(){{"Acrobatics",Ability.Dexterity},{"Animal Handling",Ability.Wisdom},{"Arcana",Ability.Intelligence},{"Athletics",Ability.Strength},{"Deception",Ability.Charisma},{"History",Ability.Intelligence},{"Insight",Ability.Wisdom},{"Intimidation",Ability.Charisma},{"Investigation",Ability.Intelligence},{"Medicine",Ability.Wisdom},{"Nature",Ability.Intelligence},{"Perception",Ability.Wisdom},{"Performance",Ability.Charisma},{"Persuasion",Ability.Charisma},{"Religion",Ability.Intelligence},{"Sleight of Hand",Ability.Dexterity},{"Stealth",Ability.Dexterity},{"Survival",Ability.Wisdom}};
    /// <summary>The sheet a canonical character document shows, such as an old save's own copy.</summary>
    public static DerivedSheet DerivedFrom(JsonObject? raw)=>raw is null?new():Derive(raw,raw["folio_sheet"]?["feature_details"],raw["folio_sheet"]?["spells"]?["spellbook"]);
    /// <summary>The rules' sheet output, read once into typed values. Imported characters may carry older shapes.</summary>
    private static DerivedSheet Derive(JsonObject raw,JsonNode? featureDetails,JsonNode? spellbook)
    {
        var metadata=raw["folio_play_metadata"];
        var attacks=new List<WeaponAttack>();
        foreach(var entry in (metadata?["attacks"] as JsonArray??[]).OfType<JsonObject>())
            if(Enum.TryParse<Ability>(Text(entry["ability"]),true,out var ability)&&Enum.IsDefined(ability))
                attacks.Add((Read<WeaponAttack>(entry)??new()) with{Ability=ability,DamageModifierAdjustment=entry["damageModifierAdjustment"] is {} adjustment?Number(adjustment):Number(entry["damageModifier"])-Number(raw["ability_modifiers"]?[ability.ToString()])});
        var senses=raw["senses"] switch
        {
            JsonArray list=>list.Select(node=>node is JsonObject sense?new CharacterSense{Type=Text(sense["type"]??sense["Type"]),Range=Number(sense["range"]??sense["Range"]),Sources=Strings(sense["sources"])}:new CharacterSense{Type=Text(node)}).Where(s=>s.Type.Length>0).ToList(),
            JsonValue text when Text(text).Length>0=>[new CharacterSense{Type=Text(text)}],
            _=>[]
        };
        ArmorClassCandidate Candidate(JsonNode? c)=>new(){Id=Text(c?["id"]),Name=Text(c?["name"]),Source=Text(c?["source"]),BaseValue=Number(c?["base_value"]),Value=Number(c?["value"]),AllowsShield=c?["allows_shield"]?.GetValue<bool>()??false,ShieldApplied=c?["shield_applied"]?.GetValue<bool>()??false,DefenseBonus=Number(c?["defense_bonus"])};
        var armor=raw["armor_class_details"] is JsonObject a?new ArmorClassDetail{Value=Number(a["value"]),FormulaId=Text(a["formula_id"]),Source=Text(a["source"]),SourceName=Text(a["source_name"]),ShieldApplied=a["shield_applied"]?.GetValue<bool>()??false,DefenseBonus=Number(a["defense_bonus"]),
            MediumArmorDexterityCap=a["medium_armor_dexterity_cap"] is JsonValue cap&&cap.TryGetValue<int>(out var dexCap)?dexCap:null,Candidates=(a["candidates"] as JsonArray??[]).Select(Candidate).ToList()}:null;
        var casting=new List<SpellcastingSource>();
        foreach(var row in (raw["spellcasting_sources"] as JsonArray??[]).OfType<JsonObject>())
            if(Enum.TryParse<Ability>(Text(row["ability"]),true,out var ability)&&Enum.IsDefined(ability))
                casting.Add(new(){SourceName=Text(row["source_name"],"Spellcasting"),SourceType=Text(row["source_type"]),Ability=ability,SpellSaveDc=Number(row["spell_save_dc"]),SpellAttackBonus=Number(row["spell_attack_bonus"])});
        var details=FeatureDetails(featureDetails);var traits=Names(raw["species_traits"]);var origins=Names(raw["origin_feats"]);
        // Only descriptions the sheet can show: owned features, traits and origin feats without a structured detail.
        var shown=Strings(raw["features"]).Concat(traits).Concat(origins).Except(details.Select(d=>d.Name)).ToHashSet(StringComparer.Ordinal);
        var descriptions=(raw["feature_descriptions"] as JsonObject??[]).Select(p=>(p.Key,Text:Text(p.Value)))
            .Concat(new[]{"species_traits","origin_feats"}.SelectMany(key=>(raw[key] as JsonArray??[]).OfType<JsonObject>().Select(item=>(Key:Text(item["name"]),Text:Text(item["description"])))))
            .Where(p=>shown.Contains(p.Key)&&p.Text.Length>0).DistinctBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Text);
        return new()
        {
            Attacks=attacks,
            AttackActionCount=metadata?["attackActionCount"] is JsonValue count&&count.TryGetValue<int>(out var perAction)&&perAction is >=1 and <=4?perAction:null,
            FeatureDetails=details,FeatureDescriptions=descriptions,SpeciesTraits=traits,OriginFeats=origins,Senses=senses,ArmorClass=armor,SpellcastingSources=casting,
            Size=Text(raw["size"]),AbilityAdjustmentBonuses=Enum.GetValues<Ability>().Where(a=>raw["ability_score_adjustment_bonuses"]?[a.ToString()] is JsonValue).ToDictionary(a=>a,a=>Number(raw["ability_score_adjustment_bonuses"]?[a.ToString()])),Languages=ProfileList(raw["languages"]),ArmorProficiencies=ProfileList(raw["armor_proficiencies"]),WeaponProficiencies=ProfileList(raw["weapon_proficiencies"]),ToolProficiencies=ProfileList(raw["tool_proficiencies"]),
            Spellbook=Strings(spellbook),GrantedSpells=GrantUses(metadata)
        };
        static List<string> Names(JsonNode? node)=>(node as JsonArray??[]).Select(n=>n is JsonObject item?Text(item["name"]):Text(n)).Where(n=>n.Length>0).Distinct().ToList();
    }
    /// <summary>A list of names, or an imported comma- or line-separated text.</summary>
    private static List<string> ProfileList(JsonNode? node)=>node is JsonArray?Strings(node).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        :Text(node).Split([',','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList();
    private static string GrantOwner(JsonNode? metadata,string grantId)=>Text((metadata?["grantSources"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(s=>Text(s["grantId"])==grantId)?["sourceId"]);
    private static List<GrantedSpellUse> GrantUses(JsonNode? metadata)
        =>[..(metadata?["grants"] as JsonArray??[]).OfType<JsonObject>().Select(g=>new GrantedSpellUse{Id=Text(g["id"]),ResourceId=Text(g["resourceId"]),BaseLevel=Number(g["baseLevel"]),Source=Text(g["source"]),OwnerId=GrantOwner(metadata,Text(g["id"]))})];
    public static List<FeatureDetail> FeatureDetails(JsonNode? rows)
        =>(rows as JsonArray??[]).OfType<JsonObject>().Select(d=>new FeatureDetail{Name=Text(d["name"]),Description=Text(d["description"]),Source=Text(d["source"]),RelatedFeatures=(d["related_features"] as JsonArray??[]).Select(r=>r is JsonObject o?new RelatedFeature{Name=Text(o["name"]),Cost=Text(o["cost"]),Description=Text(o["description"])}:new RelatedFeature{Name=Text(r)}).Where(r=>r.Name.Length>0).ToList()}).Where(d=>d.Name.Length>0).ToList();
    private static List<EquipmentItem> ResolveEquipment(JsonObject raw,CharacterState? existing)
    {
        var equipment=existing?.Equipment??Read<List<EquipmentItem>>(raw["folio_inventory"]??raw["folio_derived_inventory"]);
        if(equipment is not null)return equipment;
        equipment=[];
        foreach(var name in Strings(raw["equipment"]??raw["custom_equipment"]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var modifiers=new Dictionary<Ability,int>();
            foreach(var ability in Enum.GetValues<Ability>())
                if(raw["equipment_modifier_bonuses"]?[name]?[ability.ToString()] is {} node)modifiers[ability]=Number(node);
            equipment.Add(new(){Id=Guid.NewGuid().ToString("N"),Name=name,Equipped=true,ModifierBonuses=modifiers,
                Description="Imported equipment. Confirm its quantity and activation before play.",Consumable=name.Contains("potion",StringComparison.OrdinalIgnoreCase)||name.Contains("arrows",StringComparison.OrdinalIgnoreCase)});
        }
        return equipment;
    }
    private static T? Read<T>(JsonNode? node)=>node is null?default:node.Deserialize<T>(Json);
    private static string Text(JsonNode? node,string fallback="")=>node is JsonValue value&&value.TryGetValue<string>(out var text)?text:fallback;
    private static int Number(JsonNode? node,int fallback=0)=>node is JsonValue value&&value.TryGetValue<int>(out var number)?number:fallback;
    private static List<string> Strings(JsonNode? node)=>node is JsonArray array?array.Select(x=>Text(x)).Where(x=>x.Length>0).ToList():[];
    private static string Slug(string value)
    {
        var slug=Regex.Replace(value.ToLowerInvariant().Replace("’",""),"[^a-z0-9]+","-").Trim('-');
        if(slug.Length is >0 and <=56)return slug;
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..10].ToLowerInvariant();
        return (slug.Length==0?"spell":slug[..Math.Min(45,slug.Length)])+"-"+hash;
    }
    private static string NormalizeName(string name)=>Regex.Replace(name.ToLowerInvariant(),"[^a-z0-9]","");
    /// <summary>A catalog spell as the sheet uses it; a 2024 spell takes Herohold's modeled numbers for the same name.</summary>
    internal static SpellDefinition MapSpell(CatalogSpell spell,string castingSource)
    {
        var edition=spell.Source;var modern=edition is "XPHB" or "2024";var known=modern?SpellCatalog.All.FirstOrDefault(s=>NormalizeName(s.Name)==NormalizeName(spell.Name)):null;
        return (known??new SpellDefinition{Id=Slug(spell.Name),Name=spell.Name,Level=spell.Level}) with{
            AttackRollKind=spell.SpellAttackKind is {Length:>0} attackKind?attackKind:known?.AttackRollKind,AttackPattern=modern&&Enum.TryParse<SpellAttackPattern>(spell.SpellAttackPattern,out var attackPattern)?attackPattern:known?.AttackPattern??SpellAttackPattern.Unmodeled,
            CastingSource=castingSource,RulesSource=spell.Source,School=spell.School,
            Components=[..spell.Components.Where(x=>x is "V" or "S" or "M").Distinct()],ComponentsKnown=true,
            MaterialDescription=spell.MaterialDescription,MaterialCostCp=spell.MaterialCostCp,MaterialConsumed=spell.MaterialConsumed,MaterialConsumptionOptional=spell.MaterialConsumptionOptional,DurationDescription=spell.Duration,
            Name=spell.Name,Description=spell.Description,Source=spell.Source,Range=spell.Range,CastingTime=spell.CastingTime,
            Ritual=spell.Ritual,Concentration=known?.Concentration??spell.Duration.Contains("concentration",StringComparison.OrdinalIgnoreCase)};
    }
    public static JsonObject ToCanonical(CharacterState c)
    {
        var doc=c.Build?.ToCanonical(c.Id)??new JsonObject();
        doc["schema_version"]=5;doc["character_id"]=c.Id;doc["name"]=c.Name;
        doc["species"]=c.Species;doc["class"]=c.ClassName;doc["subclass"]=c.Subclass;doc["level"]=c.Level;
        doc["ability_scores"]=JsonSerializer.SerializeToNode(c.AbilityScores,Json);
        if(doc["base_ability_scores"] is null)doc["base_ability_scores"]=JsonSerializer.SerializeToNode(c.AbilityScores,Json);
        doc["saving_throw_proficiencies"]=JsonSerializer.SerializeToNode(c.SaveProficiencies,Json);
        doc["skill_proficiencies"]=JsonSerializer.SerializeToNode(c.SkillProficiencies,Json);
        doc["expertise"]=JsonSerializer.SerializeToNode(c.Expertise,Json);
        var hp=doc["hit_points"]?.DeepClone().AsObject()??new JsonObject();
        hp["current"]=c.CurrentHp;hp["max"]=c.MaximumHp;hp["maximum"]=c.MaximumHp;hp["temporary"]=c.TemporaryHp;
        hp["hit_die"]="d"+c.Play.HitDieSize;doc["hit_points"]=hp;
        doc["armor_class"]=c.ArmorClass;doc["speed"]=c.Speed;
        doc["notes"]=string.Join("\n\n",c.Notes.Select(n=>n.Title+"\n"+n.Text));
        doc["include_default_equipment"]=false;
        doc["custom_equipment"]=new JsonArray(c.Equipment.Where(i=>i.Quantity>0).Select(i=>JsonValue.Create(i.Name)).ToArray());
        var bonuses=new JsonObject();
        foreach(var group in c.Equipment.Where(i=>i.Quantity>0&&i.Equipped&&(!i.RequiresAttunement||i.Attuned)).GroupBy(i=>i.Name))
        {
            var values=new JsonObject();
            foreach(var ability in Enum.GetValues<Ability>())
            {
                var amount=group.Sum(i=>i.ModifierBonuses.GetValueOrDefault(ability));
                if(amount!=0)values[ability.ToString()]=amount;
            }
            if(values.Count>0)bonuses[group.Key]=values;
        }
        doc["equipment_modifier_bonuses"]=bonuses;
        if(c.Play.Spells.Count>0)
        {
            var spells=doc["spells"]?.DeepClone().AsObject()??new JsonObject();
            JsonArray Names(IEnumerable<string> ids)=>new(ids.Select(id=>JsonValue.Create(c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Name??id)).ToArray());
            spells["prepared"]=Names(c.Play.PreparedSpellIds);spells["known"]=Names(c.Play.KnownSpellIds.Where(id=>c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Level!=0));
            spells["cantrips"]=Names(c.Play.KnownSpellIds.Where(id=>c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Level==0));
            var originalBook=c.Build?.Spells?.Spellbook??[];
            var automaticBook=c.Derived.Spellbook.Except(originalBook,StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            spells["spellbook"]=Names(c.Play.SpellbookIds.Where(id=>!automaticBook.Contains(c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Name??id)));doc["spells"]=spells;
        }
        WriteClassPlaySources(doc,c);
        doc["folio_combat"]=JsonSerializer.SerializeToNode(c.Combat,Json);
        doc["folio_character_sheet"]=JsonSerializer.SerializeToNode(c.Sheet,Json);
        if(!SheetRules.HasUnresolvedExperience(c))doc["experience_points"]=SheetRules.ExperiencePoints(c);
        doc["heroic_inspiration"]=SheetRules.HasHeroicInspiration(c);
        if(!SheetRules.HasUnresolvedCoins(c))
        {
            var coins=doc["coins"] is JsonObject oldCoins?oldCoins.DeepClone().AsObject():new JsonObject();
            foreach(var value in JsonSerializer.SerializeToNode(SheetRules.Purse(c),Json)!.AsObject())coins[value.Key]=value.Value?.DeepClone();doc["coins"]=coins;
        }
        doc["folio_inventory"]=JsonSerializer.SerializeToNode(c.Equipment,Json);
        doc["folio_play"]=JsonSerializer.SerializeToNode(c.Play,Json);
        doc["folio_resources"]=JsonSerializer.SerializeToNode(c.Resources,Json);
        doc["folio_notes"]=JsonSerializer.SerializeToNode(c.Notes,Json);
        doc["folio_effects"]=JsonSerializer.SerializeToNode(c.Effects,Json);
        doc["folio_defenses"]=JsonSerializer.SerializeToNode(c.Defenses,Json);
        foreach(var (field,kind) in new[]{("damage_resistances","resistance"),("damage_immunities","immunity"),("damage_vulnerabilities","vulnerability"),("condition_immunities","conditionImmunity")})
        {
            var current=c.Defenses.Where(d=>d.Kind==kind).ToArray();var names=current.Select(d=>d.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();doc[field]=new JsonArray(names.Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray());
            if(doc["folio_build_fact_sources"]?["manual"] is JsonObject manual)
            {
                var prior=Strings(manual[field]);var retained=prior.Where(name=>names.Contains(name,StringComparer.OrdinalIgnoreCase)).Concat(current.Where(d=>d.Source!="Character build").Select(d=>d.Type)).Distinct(StringComparer.OrdinalIgnoreCase);
                manual[field]=new JsonArray(retained.Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray());
            }
        }
        doc["folio_known_target"]=JsonSerializer.SerializeToNode(c.KnownTarget,Json);
        doc["folio_exhaustion"]=c.Exhaustion;
        doc["folio_native_standing_terms"]=JsonSerializer.SerializeToNode(c.StandingAdjustmentTerms,Json);
        return doc;
    }
    public static CharacterState WithNewIdentity(CharacterState c)
    {
        var id=Guid.NewGuid().ToString("N");
        return c with{Id=id,Play=c.Play with{UndoHistory=[],CompletedOperationIds=[],LastRoll=null,LastCast=null}};
    }
}
