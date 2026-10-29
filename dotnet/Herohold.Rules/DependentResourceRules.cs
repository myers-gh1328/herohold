using System.Collections.ObjectModel;
using Herohold.Core;
namespace Herohold.Rules;
public sealed record DependentPool(string Id,string Name,int Maximum,RecoveryRest Recovery);
public sealed record DependentResourceSummary
{
    public IReadOnlyDictionary<string,int> Values { get; init; } = new Dictionary<string,int>();
    public IReadOnlyList<DependentPool> Pools { get; init; } = [];
}
/// <summary>Reviewed named ability-dependent formulas only; never guesses formulas for unknown counters.</summary>
public static class DependentResourceRules
{
    public static DependentResourceSummary Calculate(ClassProgressionCatalog catalog,string className,string subclass,int level,IReadOnlyDictionary<Ability,int> modifiers,Ability? maneuverSaveAbility=null,IReadOnlyList<string>? feats=null)
    {
        ArgumentNullException.ThrowIfNull(catalog);ArgumentNullException.ThrowIfNull(modifiers);
        if(modifiers.Count!=6||modifiers.Any(pair=>!Enum.IsDefined(pair.Key)||pair.Value is <-100000 or >100000)
            ||maneuverSaveAbility is {} choice&&choice is not (Ability.Strength or Ability.Dexterity)
            ||feats is not null&&(feats.Count>100||feats.Any(name=>string.IsNullOrWhiteSpace(name)||name.Length>200)))throw new ArgumentException("Dependent resource inputs contain invalid bounded sources.");
        var progression=ClassProgressionRules.AtLevel(catalog,className,level,subclass);className=progression.ClassName;
        var shortName=level<3?"":catalog.Find(className).SubclassNamed(progression.SubclassName)?.ShortName??"";var pb=CharacterCalculations.ProficiencyBonus(level);
        var values=new Dictionary<string,int>();var pools=new List<DependentPool>();
        int Modifier(Ability ability)=>modifiers[ability];
        int Uses(Ability ability)=>Math.Max(1,Modifier(ability));
        void Pool(string key,string id,string name,int max,RecoveryRest recovery)
        {
            if(max>100000)throw new ArgumentException("A native resource capacity exceeds its supported bound.");
            values[key]=Math.Max(0,max);pools.Add(new(id,name,Math.Max(0,max),recovery));
        }
        if(className=="Bard")Pool("bardic_inspiration_uses","bardic-inspiration","Bardic Inspiration",Uses(Ability.Charisma),level>=5?RecoveryRest.Short:RecoveryRest.Long);
        if(className=="Ranger")
        {
            if(level>=14)Pool("natures_veil_uses","natures-veil","Nature's Veil",Uses(Ability.Wisdom),RecoveryRest.Long);
            if(shortName=="Gloom Stalker")Pool("dreadful_strike_uses","dread","Dreadful Strike",Uses(Ability.Wisdom),RecoveryRest.Long);
        }
        if(className=="Cleric")
        {
            if(shortName=="War")Pool("war_priest_uses","war-priest","War Priest",Uses(Ability.Wisdom),RecoveryRest.Short);
            if(shortName=="Light")
            {
                Pool("warding_flare_uses","warding-flare","Warding Flare",Uses(Ability.Wisdom),level>=6?RecoveryRest.Short:RecoveryRest.Long);
                if(level>=17)Pool("corona_of_light_uses","corona-of-light","Corona of Light",Uses(Ability.Wisdom),RecoveryRest.Long);
            }
        }
        if(className=="Wizard"&&shortName=="Abjurer")Pool("arcane_ward_max_hp","arcane-ward","Arcane Ward HP",2*level+Modifier(Ability.Intelligence),RecoveryRest.Manual);
        if(className=="Paladin"&&shortName=="Glory"&&level>=15)Pool("glorious_defense_uses","glorious-defense","Glorious Defense",Uses(Ability.Charisma),RecoveryRest.Long);
        if(className=="Fighter"&&shortName=="Psi Warrior"&&level>=15)values["bulwark_of_force_targets"]=Uses(Ability.Intelligence);
        if(className=="Fighter"&&shortName=="Battle Master")
        {
            var ability=maneuverSaveAbility??(Modifier(Ability.Strength)>=Modifier(Ability.Dexterity)?Ability.Strength:Ability.Dexterity);
            values["maneuver_save_dc"]=8+pb+Modifier(ability);
        }
        if(progression.Spellcasting is {} casting)
        {values["spell_save_dc"]=8+pb+Modifier(casting.Ability);values["spell_attack_bonus"]=pb+Modifier(casting.Ability);}
        if(feats?.Contains("Shield Master")==true)values["shield_master_bash_dc"]=8+pb+Modifier(Ability.Strength);
        if(feats?.Contains("Great Weapon Master")==true)values["great_weapon_master_damage_bonus"]=pb;
        return new(){Values=new ReadOnlyDictionary<string,int>(values),Pools=pools.AsReadOnly()};
    }
}
