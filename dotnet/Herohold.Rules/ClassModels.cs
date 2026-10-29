using Herohold.Core;
namespace Herohold.Rules;

public sealed record ClassIdentity(string Name, string Source, int HitDie, IReadOnlyList<string> Subclasses);
public sealed record ClassProgressionSnapshot
{
    public string ClassName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string SubclassName { get; init; } = string.Empty;
    public int Level { get; init; }
    public int HitDie { get; init; }
    public IReadOnlyList<string> FeaturesAtLevel { get; init; } = [];
    public IReadOnlyList<string> FeaturesUpToLevel { get; init; } = [];
    public IReadOnlyList<string> SubclassFeaturesAtLevel { get; init; } = [];
    public IReadOnlyList<string> SubclassFeaturesUpToLevel { get; init; } = [];
    /// <summary>The features and choices the progression table lists at this level.</summary>
    public IReadOnlyList<ProgressionFeature> ChoicesAtLevel { get; init; } = [];
    public ClassResources Resources { get; init; } = ClassResources.None;
    public SpellcastingProfile? Spellcasting { get; init; }
    public SubclassGrantSummary SubclassGrants { get; init; } = new();
}
public sealed record SpellcastingProfile
{
    public Ability Ability { get; init; }
    public string SpellList { get; init; } = string.Empty;
    public string CastingType { get; init; } = "standard";
    public IReadOnlyDictionary<int, int> Slots { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<int> AvailableSpellLevels { get; init; } = [];
    public int PreparedLimit { get; init; }
    public int KnownLimit { get; init; }
    public int SpellbookLimit { get; init; }
    public int CantripLimit { get; init; }
    public IReadOnlyList<string> GrantedCantrips { get; init; } = [];
}
public sealed record SubclassGrantSummary
{
    public IReadOnlyList<string> AlwaysPreparedSpells { get; init; } = [];
    public IReadOnlyList<string> AlwaysKnownSpells { get; init; } = [];
    public IReadOnlyList<string> GrantedCantrips { get; init; } = [];
    public ClassResources Resources { get; init; } = ClassResources.None;
    public IReadOnlyList<CatalogSpellGrant> SpellGrants { get; init; } = [];
}
