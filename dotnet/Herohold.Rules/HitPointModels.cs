namespace Herohold.Rules;

public enum HitPointMethod { Maximum, Fixed, Rolled }
public sealed record HitPointHistoryEntry(int CharacterLevel, string ClassName, HitPointMethod Method, int BaseGain);
public sealed record HitPointInput
{
    public string ClassName { get; init; } = string.Empty;
    public int Level { get; init; } = 1;
    public int HitDie { get; init; } = 8;
    public IReadOnlyDictionary<string,int> ClassHitDice {get;init;}=new Dictionary<string,int>();
    public IReadOnlyDictionary<string,int> ClassLevels {get;init;}=new Dictionary<string,int>();
    public int ConstitutionModifier { get; init; }
    public IReadOnlyList<HitPointHistoryEntry> History { get; init; } = [];
    public bool Tough { get; init; }
    public bool DwarvenToughness { get; init; }
    public int AdditionalMaximumBonus { get; init; }
    public int ImportedMaximumAdjustment { get; init; }
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
}
public sealed record HitPointContribution(int CharacterLevel, HitPointMethod Method, int BaseGain, int ConstitutionModifier, int GainAfterMinimum, int ToughBonus, int SpeciesBonus, int Total);
public sealed record HitPointSummary
{
    public int Maximum { get; init; }
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
    public int CalculatedBeforeImportAdjustment { get; init; }
    public int ImportAdjustment { get; init; }
    public IReadOnlyList<HitPointHistoryEntry> History { get; init; } = [];
    public IReadOnlyList<HitPointContribution> Contributions { get; init; } = [];
}
