using System.Text.Json.Serialization;
using Herohold.Core;
namespace Herohold.Rules;
public sealed record CharacterResourceMetadata
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Maximum { get; init; }
    public string Recovery { get; init; } = "manual";
    public string Note { get; init; } = "";
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public int? ShortRestRecoveryAmount { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public int? SlotLevel { get; init; }
}
public sealed record ClassResourceSummary
{
    /// <summary>The class's resource values by name: counts, dice, ranges, recharges, spell slots (by slot level) and flags.</summary>
    public IReadOnlyDictionary<string,ChoiceValue> DerivedResources { get; init; } = new Dictionary<string,ChoiceValue>();
    public IReadOnlyList<CharacterResourceMetadata> Metadata { get; init; } = [];
}
public sealed class ClassResourceRules(ClassProgressionCatalog classes)
{
    /// <param name="speed">The walking speed the calculation found, for features that grant an equal climb or swim speed.</param>
    public ClassResourceSummary Derive(BuildFacts facts,NativeBuildSummary build,ActiveFeatureSummary features,int speed=30)
    {
        ArgumentNullException.ThrowIfNull(facts);ArgumentNullException.ThrowIfNull(build);ArgumentNullException.ThrowIfNull(features);
        var level=facts.Level;var profile=ClassProgressionRules.AtLevel(classes,build.ClassName.Length>0?build.ClassName:facts.ClassName,level,facts.Subclass);
        var name=profile.ClassName;var raw=Table(profile.Resources);var options=facts.ClassOptions;
        var shortName=level<3?"":classes.Find(name).SubclassNamed(profile.SubclassName)?.ShortName??"";
        var pb=CharacterCalculations.ProficiencyBonus(level);raw["proficiency_bonus"]=ChoiceValue.Of(pb);
        var modifiers=build.Standing.Modifiers.ToDictionary(pair=>pair.Key,pair=>pair.Value.Total);
        var maneuverAbility=ChoiceReading.TextOf(options.Get("battle_master_save_ability"));Ability? maneuver=maneuverAbility=="Strength"?Ability.Strength:maneuverAbility=="Dexterity"?Ability.Dexterity:null;
        var featNames=(facts.Feats??[]).Select(feat=>feat.Name).Where(value=>value.Length>0).ToList();
        var dependent=DependentResourceRules.Calculate(classes,name,profile.SubclassName,level,modifiers,maneuver,featNames);
        if(name=="Wizard"){raw["arcane_recovery_levels"]=ChoiceValue.Of((level+1)/2);raw["arcane_recovery_max_slot_level"]=ChoiceValue.Of(5);}
        if(name=="Fighter")
        {
            if(level>=9){raw["indomitable_uses"]=ChoiceValue.Of(level>=17?3:level>=13?2:1);raw["indomitable_recharge"]=ChoiceValue.Of("long_rest");}
            if(level>=2){raw["action_surge_uses"]=ChoiceValue.Of(level>=17?2:1);raw["action_surge_recharge"]=ChoiceValue.Of("short_or_long_rest");}
            if(level>=5)raw["attacks_per_attack_action"]=ChoiceValue.Of(level>=20?4:level>=11?3:2);
        }
        if(name=="Paladin"&&level>=6)
        {
            var range=ChoiceValue.Of(level>=18?30:10);raw["aura_of_protection_range"]=range;raw["paladin_aura_range"]=range;if(level>=10)raw["aura_of_courage_range"]=range;
            if(level>=7)foreach(var aura in profile.SubclassFeaturesUpToLevel.Where(feature=>feature.StartsWith("Aura ",StringComparison.Ordinal)))raw[Slug(aura)+"_range"]=range;
        }
        if(name=="Cleric"&&level>=20){raw["greater_divine_intervention_wish_available"]=ChoiceValue.Of(true);raw["greater_divine_intervention_wish_recharge"]=ChoiceValue.Of("2d4 long rests");}
        if(name=="Druid"&&level>=20){raw["archdruid_wild_shape_slot_conversion"]=ChoiceValue.Of(true);raw["archdruid_wild_shape_slot_conversion_recharge"]=ChoiceValue.Of("long_rest");raw["archdruid_initiative_wild_shape_recovery"]=ChoiceValue.Of(1);}
        if(name=="Ranger")
        {if(level>=6){raw["climb_speed"]=ChoiceValue.Of(speed);raw["swim_speed"]=ChoiceValue.Of(speed);}if(level>=14)raw["natures_veil_recharge"]=ChoiceValue.Of("long_rest");if(level>=20)raw["hunters_mark_damage_die"]=ChoiceValue.Of("1d10");}
        if(name=="Rogue"&&level>=20){raw["stroke_of_luck_uses"]=ChoiceValue.Of(1);raw["stroke_of_luck_recharge"]=ChoiceValue.Of("short_or_long_rest");}
        if(name=="Sorcerer"&&level>=20){raw["arcane_apotheosis_free_metamagic_per_turn"]=ChoiceValue.Of(1);raw["arcane_apotheosis_requires_innate_sorcery"]=ChoiceValue.Of(true);}
        if(name=="Warlock"&&level>=20){raw["eldritch_master_pact_slot_recovery"]=ChoiceValue.Of("all");raw["eldritch_master_recovery_trigger"]=ChoiceValue.Of("magical_cunning");}
        if(features.OriginFeats.Contains("Lucky"))raw["luck_points"]=ChoiceValue.Of(pb);
        foreach(var pair in Table(profile.SubclassGrants.Resources))raw[pair.Key]=pair.Value;
        if(name=="Fighter"&&shortName=="Battle Master")
        {
            var known=level>=15?9:level>=10?7:level>=7?5:3;var dice=level>=15?6:level>=7?5:4;var die=level>=18?"d12":level>=10?"d10":"d8";
            raw["battle_master"]=ChoiceValue.Record([new("maneuvers_known",ChoiceValue.Of(known)),new("superiority_dice_max",ChoiceValue.Of(dice)),new("superiority_die",ChoiceValue.Of(die))]);raw["maneuvers_known"]=ChoiceValue.Of(known);raw["superiority_dice"]=ChoiceValue.Of(dice);raw["superiority_die"]=ChoiceValue.Of(die);
            raw["maneuver_save_ability"]=ChoiceValue.Of((maneuver??(modifiers[Ability.Strength]>=modifiers[Ability.Dexterity]?Ability.Strength:Ability.Dexterity)).ToString());
        }
        if(shortName=="Assassin"){raw["initiative_advantage"]=ChoiceValue.Of(true);raw["assassinate_extra_damage"]=ChoiceValue.Of(level);}
        if(shortName=="Thief"){raw["climb_speed"]=ChoiceValue.Of(speed);raw["jump_ability"]=ChoiceValue.Of("Dexterity");}
        if(name=="Cleric"&&shortName=="Life")raw["preserve_life_pool"]=ChoiceValue.Of(level*5);
        if(name=="Cleric"&&shortName=="Light"&&level>=17)raw["corona_of_light_recharge"]=ChoiceValue.Of("long_rest");
        if(name=="Wizard"&&shortName=="Diviner")raw["portent_dice"]=ChoiceValue.Of(level>=14?3:2);
        if(name=="Paladin"&&shortName=="Glory"&&level>=15)raw["glorious_defense_recharge"]=ChoiceValue.Of("long_rest");
        void Alternate(string prefix,string resource){raw[prefix+"_free_uses"]=ChoiceValue.Of(1);raw[prefix+"_recharge"]=ChoiceValue.Of("long_rest");raw[prefix+"_alternate_recovery_resource"]=ChoiceValue.Of(resource);}
        if(name=="Barbarian"&&shortName=="Berserker"&&level>=14)Alternate("intimidating_presence","Rage");
        if(name=="Rogue"&&shortName=="Soulknife"&&level>=13)Alternate("psychic_veil","Psionic Energy Die");
        if(name=="Fighter"&&shortName=="Psi Warrior"&&level>=15)Alternate("bulwark_of_force","Psionic Energy Die");
        if(name=="Paladin"&&level>=20)
        {var prefix=shortName switch{"Devotion"=>"holy_nimbus","Glory"=>"living_legend","Vengeance"=>"avenging_angel","Ancients"=>"elder_champion",_=>""};if(prefix.Length>0)Alternate(prefix,"Level 5 spell slot");}
        foreach(var pair in dependent.Values)raw[pair.Key]=ChoiceValue.Of(pair.Value);
        if(profile.Spellcasting is {} casting)
        {raw["spellcasting_ability"]=ChoiceValue.Of(casting.Ability.ToString());raw["spell_slots"]=Slots(casting.Slots);}
        foreach(var sense in features.Senses)
        {
            var prefix=sense.Type=="Blindsight"?"blindsight":sense.Type=="Truesight"?"truesight":"";if(prefix.Length==0)continue;
            raw[prefix+"_range"]=ChoiceValue.Of(sense.Range);raw[prefix+"_sources"]=ChoiceValue.ListOf(sense.Sources.Select(ChoiceValue.Of));raw[prefix+"_source"]=ChoiceValue.Of(string.Join(", ",sense.Sources));
        }
        var resistances=features.Defenses.Where(defense=>defense.Kind=="resistance").GroupBy(defense=>defense.Type).ToList();
        if(resistances.Count>0)
        {raw["damage_resistances"]=ChoiceValue.ListOf(resistances.Select(group=>ChoiceValue.Of(group.Key)));raw["damage_resistance_sources"]=ChoiceValue.Record(resistances.Select(group=>KeyValuePair.Create(group.Key,ChoiceValue.ListOf(group.Select(defense=>defense.Source).Distinct().Select(ChoiceValue.Of)))));}
        var pools=new List<CharacterResourceMetadata>();
        void Add(string id,string label,int maximum,string recovery="manual",int? shortAmount=null,int? slot=null,string note="")
        {if(maximum<=0)return;if(maximum>100000)throw new ArgumentException("A native resource capacity exceeds its supported bounds.");var item=new CharacterResourceMetadata{Id=id,Name=label,Maximum=maximum,Recovery=recovery,ShortRestRecoveryAmount=shortAmount,SlotLevel=slot,Note=note};var index=pools.FindIndex(pool=>pool.Id==id);if(index<0)pools.Add(item);else pools[index]=item;}
        var profiles=new Dictionary<string,(string Id,string Name,string Recharge,int? Amount)>{
            ["rages"]=("rage","Rage","short_or_long_rest",1),["wild_shape"]=("wild-shape","Wild Shape","short_or_long_rest",1),["second_wind"]=("second-wind","Second Wind","short_or_long_rest",1),["channel_divinity"]=("channel-divinity","Channel Divinity","short_or_long_rest",1),
            ["lay_on_hands_pool"]=("lay-on-hands","Lay on Hands","long_rest",null),["focus_points"]=("focus","Focus points","short_or_long_rest",null),["sorcery_points"]=("sorcery","Sorcery points","long_rest",null),["favored_enemy"]=("hunters","Free Hunter's Mark","long_rest",null),["luck_points"]=("luck","Luck points","long_rest",null),
            ["bardic_inspiration_uses"]=("bardic-inspiration","Bardic Inspiration",level>=5?"short_or_long_rest":"long_rest",null),["action_surge_uses"]=("action-surge","Action Surge","short_or_long_rest",null),["indomitable_uses"]=("indomitable","Indomitable","long_rest",null),
            ["superiority_dice"]=("superiority","Superiority Dice","short_or_long_rest",null),["psionic_energy_dice"]=("psionic-energy","Psionic Energy Dice","short_or_long_rest",1),["portent_dice"]=("portent","Portent dice","long_rest",null),["arcane_ward_max_hp"]=("arcane-ward","Arcane Ward HP","manual",null),
            ["war_priest_uses"]=("war-priest","War Priest","short_or_long_rest",null),["warding_flare_uses"]=("warding-flare","Warding Flare",features.Features.Contains("Improved Warding Flare")?"short_or_long_rest":"long_rest",null),["natures_veil_uses"]=("natures-veil","Nature's Veil","long_rest",null),["stroke_of_luck_uses"]=("stroke-of-luck","Stroke of Luck","short_or_long_rest",null)};
        foreach(var pair in profiles){var p=pair.Value;Add(p.Id,p.Name,Number(raw.GetValueOrDefault(pair.Key)),p.Recharge,p.Amount,note:pair.Key=="portent_dice"?"Record the new foretelling die values separately after a Long Rest.":pair.Key=="arcane_ward_max_hp"?"Restore this ward only as its feature permits; recovery is manual.":"");}
        foreach(var pair in raw.GetValueOrDefault("spell_slots")?.Fields??[])if(int.TryParse(pair.Key,out var slot))Add(name=="Warlock"?"pact":"slot"+slot,name=="Warlock"?"Pact slots":"Level "+slot+" slots",Number(pair.Value),name=="Warlock"?"short_or_long_rest":"long_rest",slot:slot);
        foreach(var pair in raw)
        {
            if(profiles.ContainsKey(pair.Key)||pair.Key=="dreadful_strike_uses"||!pair.Key.EndsWith("_uses",StringComparison.Ordinal))continue;
            var prefix=pair.Key[..^5];if(prefix.EndsWith("_free",StringComparison.Ordinal))prefix=prefix[..^5];
            var recovery=raw.GetValueOrDefault(prefix+"_recharge")?.Text??"manual";if(recovery is not ("long_rest" or "short_or_long_rest"))recovery="manual";
            var label=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(prefix.Replace('_',' '));
            Add(prefix.Replace('_','-'),label,Number(pair.Value),recovery,note:recovery=="manual"?"Refer to the feature for alternate costs and timing; adjust manually.":"");
        }
        Add("hitdice","Hit Dice",level,"long_rest");
        if(name=="Ranger"&&shortName=="Gloom Stalker")Add("dread","Dreadful Strike",Math.Max(1,modifiers[Ability.Wisdom]),"long_rest");
        if(name=="Wizard"&&features.Features.Contains("Arcane Recovery"))Add("arcane-recovery","Arcane Recovery",1,"long_rest",note:"After a Short Rest, manually restore eligible slots within the feature's level budget.");
        if(name=="Warlock"&&features.Features.Contains("Magical Cunning"))Add("magical-cunning","Magical Cunning",1,"long_rest",note:"Manually restore the Pact slots permitted by this feature after spending its use.");
        return new(){DerivedResources=raw,Metadata=pools.AsReadOnly()};
    }
    /// <summary>A catalog resource table as the class's resource values.</summary>
    private static Dictionary<string,ChoiceValue> Table(ClassResources resources)
    {
        var table=new Dictionary<string,ChoiceValue>(StringComparer.Ordinal);
        foreach(var pair in resources.Counts)table[pair.Key]=ChoiceValue.Of(pair.Value);
        foreach(var pair in resources.Dice)table[pair.Key]=ChoiceValue.Of(pair.Value);
        if(resources.SpellSlots.Count>0)table["spell_slots"]=Slots(resources.SpellSlots);
        return table;
    }
    private static ChoiceValue Slots(IReadOnlyDictionary<int,int> slots)=>ChoiceValue.Record(slots.Select(pair=>KeyValuePair.Create(pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),ChoiceValue.Of(pair.Value))));
    private static string Slug(string value)=>string.Concat(value.Select(character=>char.IsLetterOrDigit(character)?char.ToLowerInvariant(character):'_')).Trim('_');
    private static int Number(ChoiceValue? value)=>value?.Number??0;
}
