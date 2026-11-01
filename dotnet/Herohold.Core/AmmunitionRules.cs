namespace Herohold.Core;

/// <summary>
/// Ammunition is untracked unless the character turns counting on. Then each attack with an Ammunition weapon spends one
/// matching item from the pack and counts it as fired, and after a fight half of what was fired (rounded down) can be recovered.
/// </summary>
public static class AmmunitionRules
{
    public static CharacterState SetCounting(CharacterState c,bool counting)
    {
        var result=DomainCopy.Clone(c);
        result=result with{Sheet=result.Sheet with{CountAmmunition=counting,AmmunitionFired=counting?result.Sheet.AmmunitionFired:[]}};
        WorkspaceValidation.Character(result);return SheetRules.CloseInspirationWindows(result);
    }

    /// <summary>The attack's ammunition when counting is on and the weapon fires it; otherwise empty.</summary>
    public static string Counted(CharacterState c,string attackId)
        =>c.Sheet.CountAmmunition?c.Derived.Attacks.FirstOrDefault(a=>a.Id==attackId)?.Ammunition??"":"";

    /// <summary>How many of this ammunition the character carries.</summary>
    public static int Carried(CharacterState c,string ammunition)=>c.Equipment.Where(i=>i.Carried&&Is(i,ammunition)).Sum(i=>i.Quantity);

    /// <summary>Spends one ammunition for an attack from <paramref name="attackId"/> ("weapon:longbow"), when counting is on.</summary>
    public static CharacterState Spend(CharacterState c,string attackId)
    {
        var ammunition=Counted(c,attackId);
        if(ammunition.Length==0)return c;
        var item=c.Equipment.FirstOrDefault(i=>i.Carried&&i.Quantity>0&&Is(i,ammunition))??throw new ArgumentException($"No {Plural(ammunition)} left. Add more in Inventory, or turn off ammunition counting.");
        var result=WorkspaceRules.AdjustQuantity(c,item.Id,-1);
        var fired=new Dictionary<string,int>(result.Sheet.AmmunitionFired){[ammunition]=result.Sheet.AmmunitionFired.GetValueOrDefault(ammunition)+1};
        return result with{Sheet=result.Sheet with{AmmunitionFired=fired}};
    }

    /// <summary>After a fight: half the fired ammunition, rounded down, goes back into the pack and the count starts over.</summary>
    public static CharacterState Recover(CharacterState c,string ammunition)
    {
        if(!c.Sheet.AmmunitionFired.TryGetValue(ammunition,out var fired))throw new ArgumentException("No fired ammunition to recover.");
        var result=DomainCopy.Clone(c);
        var item=result.Equipment.Where(i=>Is(i,ammunition)).OrderByDescending(i=>i.Carried).FirstOrDefault()??throw new ArgumentException($"Add {Plural(ammunition)} to Inventory before recovering them.");
        if(fired/2>0)result=WorkspaceRules.AdjustQuantity(result,item.Id,fired/2);
        var left=new Dictionary<string,int>(result.Sheet.AmmunitionFired);left.Remove(ammunition);
        result=result with{Sheet=result.Sheet with{AmmunitionFired=left}};
        WorkspaceValidation.Character(result);return SheetRules.CloseInspirationWindows(result);
    }

    /// <summary>An item is this ammunition when it is the catalog item or carries its name, singular or plural ("Arrow", "Arrows").</summary>
    public static bool Is(EquipmentItem item,string ammunition)
        =>item.CatalogItemId.EndsWith("|"+ammunition,StringComparison.OrdinalIgnoreCase)||item.Name.Equals(ammunition,StringComparison.OrdinalIgnoreCase)||item.Name.Equals(ammunition+"s",StringComparison.OrdinalIgnoreCase);

    public static string Plural(string ammunition)=>ammunition.EndsWith('s')?ammunition:ammunition+"s";
}
