using Herohold.Core;
namespace Herohold.Rules;
public sealed record AttackActionMetadata
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public Ability Ability { get; init; }
    public IReadOnlyList<Ability> AbilityCandidates { get; init; } = [];
    public bool Proficient { get; init; }
    public int FlatBonus { get; init; }
    public DiceFormula? DamageDice { get; init; }
    public int? FixedDamage { get; init; }
    public int DamageModifier { get; init; }
    public string DamageType { get; init; } = "";
    public string Range { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Kind { get; init; } = "weapon";
    public int AttackBonus { get; init; }
}
public sealed record AttackActionSummary
{
    public IReadOnlyList<AttackActionMetadata> Attacks { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
