using Herohold.Core;
namespace Herohold.Rules;

public static class ArmorRules
{
    internal sealed record Armor(string Name, int Base, string Category);
    internal static readonly IReadOnlyList<Armor> Standard = Array.AsReadOnly(new[] {
        new Armor("Padded Armor",11,"light"),new("Leather Armor",11,"light"),new("Studded Leather Armor",12,"light"),
        new("Hide Armor",12,"medium"),new("Chain Shirt",13,"medium"),new("Scale Mail",14,"medium"),new("Breastplate",14,"medium"),new("Half Plate Armor",15,"medium"),
        new("Ring Mail",14,"heavy"),new("Chain Mail",16,"heavy"),new("Splint Armor",17,"heavy"),new("Plate Armor",18,"heavy") });

    internal static Armor? Recognize(string name) => Standard.FirstOrDefault(a=>a.Name.Equals(name.Trim(),StringComparison.OrdinalIgnoreCase)
        ||a.Name.EndsWith(" Armor",StringComparison.Ordinal)&&a.Name[..^6].Equals(name.Trim(),StringComparison.OrdinalIgnoreCase));
    internal static bool IsShield(string name)=>name.Trim().Equals("Shield",StringComparison.OrdinalIgnoreCase);
    internal static IReadOnlyList<EquipmentItem> Active(IReadOnlyList<EquipmentItem> equipment)
    {
        if(equipment is null||equipment.Count>500)throw new ArgumentException("Armor and movement need a bounded inventory.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in equipment)
            if(item is null||string.IsNullOrWhiteSpace(item.Id)||item.Id.Length>64||!ids.Add(item.Id)||string.IsNullOrWhiteSpace(item.Name)||item.Name.Length>200||item.Quantity is <0 or >100000)
                throw new ArgumentException("Armor and movement need valid, unique inventory records.");
        return equipment.Where(i=>i.Quantity>0&&i.Equipped&&(!i.RequiresAttunement||i.Attuned)).ToArray();
    }
    internal static void ValidateIdentity(string name,int level)
    {
        if(name is null||name.Length>200||level is <1 or >20)throw new ArgumentException("Choose a supported class name and character level.");
    }

    public static ArmorSummary Calculate(ArmorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);ValidateIdentity(input.ClassName,input.Level);
        if(input.Subclass is null||input.Subclass.Length>200||input.DexterityScore is <1 or >30
            ||new[]{input.DexterityModifier,input.ConstitutionModifier,input.WisdomModifier,input.CharismaModifier}.Any(m=>m is <-20000 or >20000))
            throw new ArgumentException("Armor needs bounded scores and resolved ability modifiers.");
        var active=Active(input.Equipment);
        var armor=active.Select(i=>Recognize(i.Name)).Where(a=>a is not null).Cast<Armor>().DistinctBy(a=>a.Name).ToArray();
        var shield=active.Any(i=>IsShield(i.Name));
        var mediumCap=input.MediumArmorMaster&&input.DexterityScore>=16?3:2;
        var defense=input.DefenseStyle&&armor.Length>0?1:0;
        var candidates=new List<ArmorCandidate>();
        void Add(string name,string source,int basis,IEnumerable<CalculationTerm> additional,bool allowsShield)
        {
            var terms=new List<CalculationTerm>{new(name+" base",basis)};terms.AddRange(additional);
            if(defense>0)terms.Add(new("Defense fighting style",defense));
            if(shield&&allowsShield)terms.Add(new("Shield",2));
            candidates.Add(new(name,source,new(terms.Sum(t=>t.Value),terms.AsReadOnly()),shield&&allowsShield,defense,allowsShield));
        }
        foreach(var piece in armor)
        {
            var dex=piece.Category=="heavy"?0:piece.Category=="medium"?Math.Min(input.DexterityModifier,mediumCap):input.DexterityModifier;
            Add(piece.Name,"armor",piece.Base,piece.Category=="heavy"?[]:[new CalculationTerm(piece.Category=="medium"?"Dexterity modifier · cap "+mediumCap:"Dexterity modifier",dex)],true);
        }
        if(armor.Length==0)
        {
            var dex=new CalculationTerm("Dexterity modifier",input.DexterityModifier);
            Add("Unarmored","base",10,[dex],true);
            if(input.ClassName.Equals("Barbarian",StringComparison.OrdinalIgnoreCase))
                Add("Barbarian Unarmored Defense","class_feature",10,[dex,new("Constitution modifier",input.ConstitutionModifier)],true);
            if(input.ClassName.Equals("Monk",StringComparison.OrdinalIgnoreCase)&&!shield)
                Add("Monk Unarmored Defense","class_feature",10,[dex,new("Wisdom modifier",input.WisdomModifier)],false);
            if(input.Level>=3&&input.ClassName.Equals("Bard",StringComparison.OrdinalIgnoreCase)&&input.Subclass.Equals("College of Dance",StringComparison.OrdinalIgnoreCase)&&!shield)
                Add("College of Dance Unarmored Defense","subclass_feature",10,[dex,new("Charisma modifier",input.CharismaModifier)],false);
            if(input.Level>=3&&input.ClassName.Equals("Sorcerer",StringComparison.OrdinalIgnoreCase)&&input.Subclass.Equals("Draconic Sorcery",StringComparison.OrdinalIgnoreCase))
                Add("Draconic Resilience","subclass_feature",10,[dex,new("Charisma modifier",input.CharismaModifier)],true);
        }
        return new(candidates.OrderByDescending(c=>c.Calculation.Total).First(),candidates.AsReadOnly(),mediumCap);
    }
}
public static class MovementRules
{
    public static MovementSummary Calculate(MovementInput input)
    {
        ArgumentNullException.ThrowIfNull(input);ArmorRules.ValidateIdentity(input.ClassName,input.Level);
        if(input.BaseSpeed is <0 or >10000||input.UnarmoredMovementBonus is <0 or >1000||input.Feats is null||input.Feats.Count>100||input.Feats.Any(f=>f is null||f.Length>200))
            throw new ArgumentException("Movement needs bounded speed, class bonuses, and feat names.");
        var active=ArmorRules.Active(input.Equipment);
        var armor=active.Select(i=>ArmorRules.Recognize(i.Name)).Where(a=>a is not null).ToArray();
        var shield=active.Any(i=>ArmorRules.IsShield(i.Name));
        var heavy=armor.Any(a=>a!.Category=="heavy");
        var feats=input.Feats.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var terms=new List<MovementTerm>();
        if(feats.Contains("Speedy"))terms.Add(new("Speedy",10,true));
        if(feats.Contains("Boon of Speed"))terms.Add(new("Boon of Speed",30,true));
        if(input.ClassName.Equals("Barbarian",StringComparison.OrdinalIgnoreCase)&&input.Level>=5)terms.Add(new("Fast Movement",10,!heavy));
        var roving=input.ClassName.Equals("Ranger",StringComparison.OrdinalIgnoreCase)&&input.Level>=6;
        if(roving)terms.Add(new("Roving",10,!heavy));
        if(input.ClassName.Equals("Monk",StringComparison.OrdinalIgnoreCase)&&input.Level>=2&&input.UnarmoredMovementBonus>0)
            terms.Add(new("Unarmored Movement",input.UnarmoredMovementBonus,armor.Length==0&&!shield));
        var calculationTerms=new List<CalculationTerm>{new("Species base speed",input.BaseSpeed)};
        calculationTerms.AddRange(terms.Where(t=>t.Applies).Select(t=>new CalculationTerm(t.Name,t.Amount)));
        var total=calculationTerms.Sum(t=>t.Value);
        return new(total,roving?total:null,roving?total:null,new(total,calculationTerms.AsReadOnly()),terms.AsReadOnly());
    }
}
