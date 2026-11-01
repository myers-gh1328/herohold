using Herohold.Core;
using System.Text.RegularExpressions;
namespace Herohold.Rules;
public sealed class AttackActionRules(WeaponCapabilityCatalog weapons)
{
    public AttackActionSummary Derive(BuildFacts facts,NativeBuildSummary build,ActiveFeatureSummary features,ClassResourceSummary resources,IReadOnlyList<EquipmentItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(facts);ArgumentNullException.ThrowIfNull(build);ArgumentNullException.ThrowIfNull(features);ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(equipment);
        if(equipment.Count>500)throw new ArgumentException("Attack equipment exceeds the workspace limit.");
        var active=equipment.Where(item=>item.Quantity>0&&item.Equipped).ToArray();
        // An item attacks as its base weapon when it names one (a named or magic weapon), otherwise as the catalog weapon of its own name.
        var wielded=active.Select(item=>(Item:item,Weapon:weapons.Weapons.FirstOrDefault(weapon=>weapon.Name.Equals(WorksAs(item),StringComparison.OrdinalIgnoreCase)))).Where(pair=>pair.Weapon is not null).Select(pair=>(pair.Item,Weapon:pair.Weapon!)).ToArray();
        var equipped=wielded.Select(pair=>pair.Weapon).ToArray();
        var level=facts.Level;if(level is <1 or >20)throw new ArgumentException("Attack level must be between 1 and 20.");
        var proficiency=2+(level-1)/4;
        var monk=build.ClassName=="Monk"&&!active.Any(item=>ArmorRules.IsShield(item.Name)||ArmorRules.Recognize(item.Name) is not null)&&equipped.All(MonkWeapon);
        var martial=monk?Dice(resources.DerivedResources.GetValueOrDefault("martial_arts_die")?.Text??""):null;
        var styles=FightingStyleRules.ActiveStyles(facts).ToHashSet(StringComparer.Ordinal);
        var proficiencies=features.Proficiencies.Concat(build.ProficiencySources).Where(source=>source.Kind==ProficiencySourceKind.Weapon).ToArray();
        var masteries=(facts.WeaponMasteries??[]).ToHashSet(StringComparer.Ordinal);
        foreach(var feat in (facts.Feats??[]).Where(feat=>feat.Name=="Weapon Master"))
            if(feat.Choices.Get("weapon_mastery")?.Text is {} name)masteries.Add(name);
        var attacks=new List<AttackActionMetadata>();var warnings=new List<string>();
        foreach(var item in active.Where(item=>!string.IsNullOrEmpty(item.BaseWeapon)&&!wielded.Any(pair=>pair.Item==item)).DistinctBy(item=>item.Name,StringComparer.OrdinalIgnoreCase))
            warnings.Add($"{item.Name} works as {item.BaseWeapon}, which is not a weapon in the catalog; it has no attack.");
        foreach(var (item,weapon) in wielded.DistinctBy(pair=>pair.Item.Name,StringComparer.OrdinalIgnoreCase))
        {
            var twoHanded=weapon.TwoHanded||facts.Presentation?.WeaponWieldingModes.GetValueOrDefault(item.Name)=="two_handed";
            var damage=Dice(twoHanded&&!string.IsNullOrEmpty(weapon.VersatileDamage)?weapon.VersatileDamage:weapon.Damage);
            if(damage is null){warnings.Add($"{item.Name} uses fixed or unmodeled damage; resolve its damage manually.");continue;}
            if(martial is not null&&MonkWeapon(weapon)&&Average(martial)>Average(damage))damage=martial;
            Ability[] candidates=weapon.Finesse||monk&&MonkWeapon(weapon)?[Ability.Strength,Ability.Dexterity]:weapon.Ranged?[Ability.Dexterity]:[Ability.Strength];
            var ability=candidates.OrderByDescending(Modifier).First();
            var proficient=proficiencies.Any(source=>Proficient(source,weapon));
            var flat=(styles.Contains("Archery")&&weapon.Ranged?2:0)+item.AttackBonus;
            var dueling=styles.Contains("Dueling")&&weapon.Melee&&!twoHanded&&equipped.Length==1?2:0;
            var notes=new List<string>();
            if(item.AttackBonus!=0||item.DamageBonus!=0)notes.Add($"{item.Name}: {item.AttackBonus:+0;-0;+0} to attack rolls and {item.DamageBonus:+0;-0;+0} to damage.");
            if(dueling>0)notes.Add("Dueling: +2 damage while holding this melee weapon in one hand and no other weapons.");
            if(features.OriginFeats.Contains("Savage Attacker",StringComparer.Ordinal))notes.Add("Savage Attacker: once per turn on a weapon hit, roll the weapon's damage dice twice and choose either set; resolve this choice manually.");
            if(styles.Contains("Great Weapon Fighting")&&twoHanded&&weapon.Melee)notes.Add("Great Weapon Fighting: treat damage-die results of 1 or 2 as 3 while attacking with this two-handed or versatile weapon in two hands; apply this manually.");
            if(styles.Contains("Thrown Weapon Fighting"))notes.Add("Thrown Weapon Fighting: add +2 damage when a weapon with the Thrown property hits as a ranged attack; apply this manually when throwing it.");
            if(styles.Contains("Two-Weapon Fighting")&&weapon.Light)notes.Add("Two-Weapon Fighting: add the ability modifier to the extra Light-property attack's damage; resolve that separate attack manually.");
            if(masteries.Contains(weapon.Name)&&weapon.MasteryName.Length>0)notes.Add("Weapon Mastery: "+weapon.MasteryName+". Apply its conditions and effects manually.");
            attacks.Add(new(){Id="weapon:"+Regex.Replace(item.Name.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-'),Name=item.Name,Ability=ability,AbilityCandidates=Array.AsReadOnly(candidates),Proficient=proficient,FlatBonus=flat,DamageDice=damage,DamageModifier=Modifier(ability)+dueling+item.DamageBonus,DamageType=DamageType(weapon.DamageType),Range=weapon.Range,Ammunition=weapon.Ammunition,AttackBonus=Modifier(ability)+(proficient?proficiency:0)+flat,Notes=string.Join(" ",notes)});
        }
        var unarmed=martial;var unarmedStyle=styles.Contains("Unarmed Fighting");var brawler=features.OriginFeats.Contains("Tavern Brawler",StringComparer.Ordinal);
        if(unarmedStyle){var styleDie=new DiceFormula(1,equipped.Length==0&&!active.Any(item=>ArmorRules.IsShield(item.Name))?8:6);if(unarmed is null||Average(styleDie)>Average(unarmed))unarmed=styleDie;}
        if(brawler&&(unarmed is null||Average(new(1,4))>Average(unarmed)))unarmed=new(1,4);
        {
            Ability[] candidates=monk?[Ability.Strength,Ability.Dexterity]:[Ability.Strength];var ability=candidates.OrderByDescending(Modifier).First();var notes=new List<string>();
            var saveDc=8+proficiency+Modifier(ability);
            notes.Add($"Choose damage, Grapple or Shove. Grapple/Shove: the target chooses Strength or Dexterity save against DC {saveDc}; resolve size, reach and free-hand requirements at the table.");
            if(martial is not null)notes.Add("Martial Arts; requires no armor or shield and only Monk weapons.");
            if(unarmedStyle)notes.Add("Unarmed Fighting; use d8 only while holding no weapon or Shield. Resolve its start-of-turn grapple damage manually.");
            if(brawler)notes.Add("Tavern Brawler; resolve damage-die rerolls and the once-per-turn push manually.");
            attacks.Add(new(){Id="unarmed",Name="Unarmed Strike",Kind="unarmed",Ability=ability,AbilityCandidates=Array.AsReadOnly(candidates),Proficient=true,DamageDice=unarmed,FixedDamage=unarmed is null?1:null,DamageModifier=Modifier(ability),DamageType="Bludgeoning",Range="Melee reach",AttackBonus=Modifier(ability)+proficiency,Notes=string.Join(" ",notes)});
        }
        return new(){Attacks=attacks.AsReadOnly(),Warnings=warnings.AsReadOnly()};
        int Modifier(Ability ability)=>build.Standing.Modifiers.TryGetValue(ability,out var value)?value.Total:throw new ArgumentException("All six native ability modifiers are required.");
    }
    private static string WorksAs(EquipmentItem item)=>string.IsNullOrEmpty(item.BaseWeapon)?item.Name:item.BaseWeapon;
    private static bool MonkWeapon(WeaponCapabilityFact weapon)=>weapon.Melee&&(weapon.Category=="Simple"||weapon.Category=="Martial"&&weapon.Light);
    private static bool Proficient(ChoiceProficiencySource source,WeaponCapabilityFact weapon)=>source.Target==weapon.Name||source.Target=="Simple Weapons"&&weapon.Category=="Simple"||source.Target=="Martial Weapons"&&weapon.Category=="Martial"||source.SourceId=="class:Monk"&&weapon.Category=="Martial"&&weapon.Light||source.SourceId=="class:Rogue"&&weapon.Category=="Martial"&&(weapon.Light||weapon.Finesse);
    private static double Average(DiceFormula dice)=>dice.Count*(dice.Sides+1)/2d;
    private static DiceFormula? Dice(string value)
    {
        var match=Regex.Match(value,"^([0-9]*)d([0-9]+)$",RegexOptions.CultureInvariant);
        if(!match.Success||!int.TryParse(match.Groups[1].Value.Length==0?"1":match.Groups[1].Value,out var count)||!int.TryParse(match.Groups[2].Value,out var sides)||count is <1 or >100||sides is <2 or >100)return null;
        return new(count,sides);
    }
    private static string DamageType(string code)=>code switch{"A"=>"Acid","B"=>"Bludgeoning","C"=>"Cold","F"=>"Fire","L"=>"Lightning","N"=>"Necrotic","P"=>"Piercing","Po"=>"Poison","R"=>"Radiant","S"=>"Slashing","T"=>"Thunder","Y"=>"Psychic",_=>code};
}
