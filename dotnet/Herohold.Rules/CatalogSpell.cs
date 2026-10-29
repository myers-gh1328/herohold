namespace Herohold.Rules;

/// <summary>A spell as the rules catalog describes it, read once when the catalog loads.</summary>
public sealed record CatalogSpell
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "XPHB";
    public int Level { get; init; }
    public string School { get; init; } = "";
    public string SchoolCode { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Classes { get; init; } = [];
    public string CastingTime { get; init; } = "";
    public string Range { get; init; } = "";
    /// <summary>"V", "S" and "M" as the spell needs them; from the raw catalog when it describes the spell.</summary>
    public IReadOnlyList<string> Components { get; init; } = [];
    public bool ComponentsKnown { get; init; }
    public string Duration { get; init; } = "";
    public bool Ritual { get; init; }
    public string MaterialDescription { get; init; } = "";
    public int? MaterialCostCp { get; init; }
    public bool? MaterialConsumed { get; init; }
    public bool MaterialConsumptionOptional { get; init; }
    /// <summary>"ranged", "melee" or "mixed" for a spell attack; unset for a spell without one.</summary>
    public string? SpellAttackKind { get; init; }
    public string? SpellAttackPattern { get; init; }
}

/// <summary>A weapon as the rules catalog lists it, read once when the catalog loads.</summary>
public sealed record CatalogWeapon(string Name,string Category,string Damage,string DamageType,string Mastery);
