using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
public enum WeaponProficiencyScope { Named, Simple, Martial, MartialLight, MartialFinesseOrLight }
public sealed record WeaponProficiencyRule(WeaponProficiencyScope Scope,string WeaponName="");
public sealed record WeaponCapabilityFact(string Name,string Category,bool Light,bool Finesse,bool Mastery,bool Melee,bool Ranged,bool TwoHanded,string Damage,string VersatileDamage,string DamageType,string Range)
{public string MasteryName {get;init;}="";}
public sealed class WeaponCapabilityCatalog
{
    private readonly IReadOnlyList<WeaponCapabilityFact> records;
    public IReadOnlyList<WeaponCapabilityFact> Weapons=>records;
    private WeaponCapabilityCatalog(IReadOnlyList<WeaponCapabilityFact> records)=>this.records=records;
    public static WeaponCapabilityCatalog Parse(string rawItemsBaseJson,RulesCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if(string.IsNullOrWhiteSpace(rawItemsBaseJson)||Encoding.UTF8.GetByteCount(rawItemsBaseJson)>32*1024*1024)throw new ArgumentException("The weapon capability catalog is empty or too large.");
        JsonObject raw;
        try{raw=JsonNode.Parse(rawItemsBaseJson,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new ArgumentException("The raw weapon catalog must be an object.");}
        catch(JsonException error){throw new ArgumentException("The weapon capability catalog is malformed.",error);}
        if(raw["baseitem"] is not JsonArray array||array.Count>50000)throw new ArgumentException("Weapon source records must be a bounded array.");
        var lookup=new Dictionary<string,JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in array.OfType<JsonObject>().Where(item=>item["source"]?.GetValue<string>()=="XPHB"))
        {var name=item["name"]?.GetValue<string>()??"";if(name.Length>0)lookup[name]=item;}
        var records=new List<WeaponCapabilityFact>();
        foreach(var weapon in catalog.WeaponRecords)
        {
            var name=weapon.Name;if(string.IsNullOrWhiteSpace(name)||name.Length>200)throw new ArgumentException("Weapon identities are invalid.");
            lookup.TryGetValue(name,out var item);
            // Trusted normalized aliases in the weapon catalog identify the same Quarterstaff profile.
            if(name is "Staff" or "Wooden Staff")lookup.TryGetValue("Quarterstaff",out item);
            var category=item?["weaponCategory"]?.GetValue<string>() switch{"simple"=>"Simple","martial"=>"Martial",_=>weapon.Category};
            var properties=(item?["property"] as JsonArray??new()).Select(node=>node is JsonObject property?property["uid"]?.GetValue<string>()??"":node?.GetValue<string>()??"").Select(value=>value.Split('|')[0]).ToHashSet(StringComparer.Ordinal);
            var psychic=name=="Psychic Blade";var type=item?["type"]?.GetValue<string>().Split('|')[0]??"";
            records.Add(new(name,category,properties.Contains("L"),properties.Contains("F")||psychic,!string.IsNullOrWhiteSpace(weapon.Mastery),type=="M"||psychic,type=="R",properties.Contains("2H"),weapon.Damage,item?["dmg2"]?.GetValue<string>()??"",weapon.DamageType,type=="R"?"Ranged weapon":"Melee reach"){MasteryName=weapon.Mastery});
        }
        return new(records.AsReadOnly());
    }
    public IReadOnlyList<string> EligibleMasteries(IReadOnlyList<WeaponProficiencyRule> proficiencies)
    {
        ArgumentNullException.ThrowIfNull(proficiencies);
        if(proficiencies.Count>1000||proficiencies.Any(rule=>rule is null||!Enum.IsDefined(rule.Scope)||rule.WeaponName is null||rule.WeaponName.Length>200||rule.Scope==WeaponProficiencyScope.Named&&string.IsNullOrWhiteSpace(rule.WeaponName)))throw new ArgumentException("Weapon proficiency rules contain an invalid bounded selector.");
        bool Eligible(WeaponCapabilityFact weapon)=>proficiencies.Any(rule=>rule.Scope switch
        {
            WeaponProficiencyScope.Named=>rule.WeaponName==weapon.Name,
            WeaponProficiencyScope.Simple=>weapon.Category=="Simple",
            WeaponProficiencyScope.Martial=>weapon.Category=="Martial",
            WeaponProficiencyScope.MartialLight=>weapon.Category=="Martial"&&weapon.Light,
            WeaponProficiencyScope.MartialFinesseOrLight=>weapon.Category=="Martial"&&(weapon.Light||weapon.Finesse),_=>false
        });
        return records.Where(weapon=>weapon.Mastery&&Eligible(weapon)).Select(weapon=>weapon.Name).Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
    }
    public IReadOnlyList<string> EligibleClassMasteries(string className,IReadOnlyList<WeaponProficiencyRule> proficiencies)
    {
        if(string.IsNullOrWhiteSpace(className)||className.Length>200)throw new ArgumentException("Choose a bounded class identity for Weapon Mastery.");
        var eligible=EligibleMasteries(proficiencies);
        return !className.Equals("Barbarian",StringComparison.OrdinalIgnoreCase)?eligible:eligible.Where(name=>records.Any(record=>record.Name==name&&record.Melee)).ToList().AsReadOnly();
    }
}
