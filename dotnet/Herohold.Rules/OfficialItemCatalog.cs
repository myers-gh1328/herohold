using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>
/// The official 2024 items (XPHB and XDMG) from the raw rules data: name, kind, weight, text, weapon and charges. An item's id is
/// its source and name ("XPHB|Longbow"), so another rule set's items sit beside them with their own ids.
/// </summary>
public sealed partial class OfficialItemCatalog:IItemCatalog
{
    public static readonly string[] Sources=["XPHB","XDMG"];
    private readonly Dictionary<string,CatalogItem> byName;
    private readonly Dictionary<string,(string Item,int Quantity)> packs;
    private readonly IReadOnlyDictionary<string,int> spellLevels;
    public IReadOnlyList<CatalogItem> Official {get;}

    private OfficialItemCatalog(List<CatalogItem> items,Dictionary<string,(string,int)> packs,IReadOnlyDictionary<string,int> spellLevels)
    {
        Official=items.AsReadOnly();this.packs=packs;this.spellLevels=spellLevels;
        // XPHB first, so a name in both books is the Player's Handbook item.
        byName=new(StringComparer.OrdinalIgnoreCase);
        foreach(var item in items.OrderBy(i=>Array.IndexOf(Sources,i.Source)))byName.TryAdd(item.Definition.Name,item);
    }

    /// <param name="spellLevels">Spell names and levels, to read "Scroll of Speak with Dead" as a level 3 spell scroll.</param>
    public static OfficialItemCatalog Parse(string rawItemsBaseJson,string rawItemsJson,IReadOnlyDictionary<string,int> spellLevels)
    {
        var records=Records(rawItemsBaseJson,"baseitem").Concat(Records(rawItemsJson,"item")).Where(r=>Sources.Contains(Text(r["source"]))).ToList();
        var baseKinds=records.Where(r=>Text(r["source"])=="XPHB").GroupBy(r=>Text(r["name"]),StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>Kind(g.First()),StringComparer.OrdinalIgnoreCase);
        var items=new List<CatalogItem>();var packs=new Dictionary<string,(string,int)>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in records)
        {
            var name=Text(r["name"]);var source=Text(r["source"]);
            if(name.Length is 0 or >200||!ids.Add(source+"|"+name))continue;
            var kind=Kind(r);var baseName=Text(r["baseItem"]).Split('|')[0];
            var baseWeapon=baseName.Length>0&&baseKinds.TryGetValue(baseName,out var baseKind)&&baseKind=="weapon"?Title(baseName,records):"";
            if(baseWeapon.Length>0)kind="weapon";
            var weapon=Bonus(r["bonusWeapon"]);
            var healing=kind=="potion"?Healing().Match(r["entries"]?.ToJsonString(Unescaped)??""):System.Text.RegularExpressions.Match.Empty;
            items.Add(new CatalogItem{Id=source+"|"+name,Source=source,Kind=kind,Definition=new EquipmentItem
            {
                Name=name,Description=Description(r,kind),RequiresAttunement=r["reqAttune"] is not null&&r["reqAttune"]!.GetValueKind()!=JsonValueKind.False,
                UnitWeightLb=r["weight"] is JsonValue w&&w.TryGetValue<double>(out var pounds)?(decimal)pounds:null,
                Charges=r["charges"] is JsonValue c&&c.TryGetValue<int>(out var charges)&&charges>0?new ItemCharges{Maximum=charges,Remaining=0,RecoveryNote=Recharge(r)}:null,
                UseAction=kind=="potion"?ItemUseAction.BonusAction:ItemUseAction.TableManaged,
                Consumable=kind is "potion" or "scroll" or "ammunition"||(r["miscTags"] as JsonArray??[]).Any(t=>Text(t)=="CNS"),
                BaseWeapon=baseWeapon,AttackBonus=weapon+Bonus(r["bonusWeaponAttack"]),DamageBonus=weapon+Bonus(r["bonusWeaponDamage"]),
                HealingDice=healing.Success?new(int.Parse(healing.Groups[1].Value,CultureInfo.InvariantCulture),int.Parse(healing.Groups[2].Value,CultureInfo.InvariantCulture)):null,
                HealingBonus=healing.Success&&healing.Groups[3].Success?int.Parse(healing.Groups[3].Value,CultureInfo.InvariantCulture):0
            }});
            if(r["packContents"] is JsonArray contents&&contents.Count==1&&contents[0] is JsonObject only&&Text(only["item"]).Length>0&&only["quantity"] is JsonValue q&&q.TryGetValue<int>(out var quantity)&&quantity>1)
                packs[name]=(Text(only["item"]).Split('|')[0],quantity);
        }
        return new(items,packs,spellLevels);
    }

    /// <summary>
    /// The official item a name stands for: the item itself, the single item a bundle holds ("Arrows (20)" is 20 Arrows), a
    /// spell scroll ("Scroll of Speak with Dead"), the singular ("Arrows"), or the part in brackets ("Druidic Focus (sprig of mistletoe)").
    /// </summary>
    public ItemMatch? Match(string name)
    {
        ArgumentNullException.ThrowIfNull(name);name=name.Trim();
        if(name.Length is 0 or >200)return null;
        if(packs.TryGetValue(name,out var pack)&&byName.TryGetValue(pack.Item,out var held))return new(held,"",pack.Quantity);
        if(byName.TryGetValue(name,out var item))return new(item,"",1);
        if(ScrollOf().Match(name) is {Success:true} scroll&&spellLevels.TryGetValue(scroll.Groups[1].Value.Trim(),out var level)
           &&byName.TryGetValue(level==0?"Spell Scroll (Cantrip)":$"Spell Scroll (Level {level})",out var spellScroll))
            return new(spellScroll,spellLevels.Keys.First(k=>k.Equals(scroll.Groups[1].Value.Trim(),StringComparison.OrdinalIgnoreCase)),1);
        if(name.EndsWith('s')&&byName.TryGetValue(name[..^1],out var single))return new(single,"",1);
        if(Bracketed().Match(name) is {Success:true} bracketed&&byName.TryGetValue(bracketed.Groups[1].Value.Trim(),out var inner))return new(inner,"",1);
        return null;
    }

    /// <summary>A potion's "regain {@dice 2d4 + 2} Hit Points".</summary>
    [GeneratedRegex(@"regains? \{@dice (\d+)d(\d+)(?: ?\+ ?(\d+))?\} \{@variantrule Hit Points",RegexOptions.CultureInvariant)]private static partial Regex Healing();
    [GeneratedRegex(@"^Scroll of (.+)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]private static partial Regex ScrollOf();
    [GeneratedRegex(@"^[^()]+\(([^()]+)\)$",RegexOptions.CultureInvariant)]private static partial Regex Bracketed();

    private static readonly JsonSerializerOptions Unescaped=new(){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private static IEnumerable<JsonObject> Records(string json,string key)
    {
        if(string.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>32*1024*1024)throw new ArgumentException("An item catalog is empty or exceeds its supported size.");
        JsonObject root;try{root=JsonNode.Parse(json,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new ArgumentException("An item catalog must be an object.");}
        catch(JsonException error){throw new ArgumentException("The item catalog is not valid JSON.",error);}
        if(root[key] is not JsonArray records||records.Count>50000)throw new ArgumentException("Item records must be a bounded array.");
        return records.OfType<JsonObject>();
    }

    private static string Kind(JsonObject r)
    {
        if(r["weapon"] is JsonValue w&&w.TryGetValue<bool>(out var isWeapon)&&isWeapon)return "weapon";
        return Text(r["type"]).Split('|')[0] switch
        {
            "M" or "R"=>"weapon","A" or "AF"=>"ammunition","LA" or "MA" or "HA"=>"armor","S"=>"shield","P"=>"potion","SC"=>"scroll",
            "AT" or "GS" or "INS" or "T"=>"tool","SCF"=>"focus","G" or "$G" or "TG" or "FD" or "EXP"=>"gear",_=>"other"
        };
    }

    private static string Description(JsonObject r,string kind)
    {
        var lines=new List<string>();
        if(r["dmg1"] is not null)
        {
            var properties=(r["property"] as JsonArray??[]).Select(p=>Text(p).Split('|')[0]).Select(p=>p switch
                {"2H"=>"Two-Handed","A"=>"Ammunition","F"=>"Finesse","H"=>"Heavy","L"=>"Light","LD"=>"Loading","R"=>"Reach","T"=>"Thrown","V"=>"Versatile",_=>p}).ToList();
            var damage=Text(r["dmg1"])+" "+(Text(r["dmgType"]) switch{"P"=>"Piercing","S"=>"Slashing","B"=>"Bludgeoning",var t=>t});
            var parts=new List<string>{$"{Title(Text(r["weaponCategory"]))} {(Text(r["type"]).StartsWith('R')?"Ranged":"Melee")} Weapon".Trim(),damage};
            if(Text(r["dmg2"]).Length>0)parts.Add("Versatile ("+Text(r["dmg2"])+")");
            if(Text(r["range"]).Length>0)parts.Add("Range "+Text(r["range"]));
            if(properties.Count>0)parts.Add(string.Join(", ",properties));
            if(r["mastery"] is JsonArray mastery&&mastery.Count>0)parts.Add("Mastery: "+Text(mastery[0]).Split('|')[0]);
            lines.Add(string.Join(" · ",parts));
        }
        else if(kind is "armor" or "shield"&&r["ac"] is JsonValue ac&&ac.TryGetValue<int>(out var armorClass))
        {
            var type=Text(r["type"]).Split('|')[0];
            var parts=new List<string>{type switch{"LA"=>$"Light Armor · AC {armorClass} + Dex modifier","MA"=>$"Medium Armor · AC {armorClass} + Dex modifier (max 2)","HA"=>$"Heavy Armor · AC {armorClass}",_=>$"Shield · +{armorClass} AC"}};
            if(Text(r["strength"]).Length>0)parts.Add("Strength "+Text(r["strength"]));
            if(r["stealth"] is JsonValue s&&s.TryGetValue<bool>(out var stealth)&&stealth)parts.Add("Disadvantage on Stealth");
            lines.Add(string.Join(" · ",parts));
        }
        Entries(r["entries"],lines);
        return string.Join("\n",lines.Where(l=>l.Length>0));
    }

    private static void Entries(JsonNode? node,List<string> lines)
    {
        switch(node)
        {
            case JsonValue v when v.TryGetValue<string>(out var text):lines.Add(RulesCatalogSnapshot.CleanTags(text));break;
            case JsonArray array:foreach(var child in array)Entries(child,lines);break;
            case JsonObject o when Text(o["type"])=="table":
                if(Text(o["caption"]).Length>0)lines.Add(RulesCatalogSnapshot.CleanTags(Text(o["caption"])));
                foreach(var row in (o["colLabels"] is JsonArray labels?new JsonArray(labels.DeepClone()):new JsonArray()).Concat(o["rows"] as JsonArray??[]))
                    if(row is JsonArray cells)lines.Add(string.Join(" | ",cells.Select(c=>RulesCatalogSnapshot.CleanTags(c is JsonValue?Text(c):Text((c as JsonObject)?["roll"]?["exact"]??(c as JsonObject)?["entry"])))));
                    else if(row is JsonValue)lines.Add(RulesCatalogSnapshot.CleanTags(Text(row)));
                break;
            case JsonObject o:
                var start=lines.Count;
                Entries(o["entries"]??o["items"]??o["entry"],lines);
                if(Text(o["name"]) is {Length:>0} title)
                {
                    if(lines.Count>start)lines[start]=RulesCatalogSnapshot.CleanTags(title)+". "+lines[start];else lines.Add(RulesCatalogSnapshot.CleanTags(title)+".");
                }
                break;
        }
    }

    private static string Recharge(JsonObject r)=>Text(r["recharge"]) switch
    {
        ""=>"",var time=>"Regains "+(Text(r["rechargeAmount"]) is {Length:>0} amount?RulesCatalogSnapshot.CleanTags(amount)+" ":"")+"charges at "+time+"."
    };
    private static int Bonus(JsonNode? node)=>int.TryParse(Text(node),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value)?value:0;
    private static string Title(string text)=>text.Length==0?text:char.ToUpperInvariant(text[0])+text[1..];
    private static string Title(string baseName,List<JsonObject> records)=>records.Select(r=>Text(r["name"])).FirstOrDefault(n=>n.Equals(baseName,StringComparison.OrdinalIgnoreCase))??baseName;
    private static string Text(JsonNode? node)=>node is JsonValue value?value.GetValueKind() switch
    {
        JsonValueKind.String=>value.GetValue<string>(),JsonValueKind.Number=>value.ToJsonString(),_=>""
    }:"";
}
