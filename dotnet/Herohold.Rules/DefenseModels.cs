using Herohold.Core;
namespace Herohold.Rules;

public sealed record ArmorInput
{
    public string ClassName { get; init; } = "";
    public string Subclass { get; init; } = "";
    public int Level { get; init; } = 1;
    public int DexterityScore { get; init; } = 10;
    public int DexterityModifier { get; init; }
    public int ConstitutionModifier { get; init; }
    public int WisdomModifier { get; init; }
    public int CharismaModifier { get; init; }
    public bool MediumArmorMaster { get; init; }
    public bool DefenseStyle { get; init; }
    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];
}
public sealed record ArmorCandidate(string Name, string Source, Calculation Calculation, bool ShieldApplied, int DefenseBonus, bool AllowsShield);
public sealed record ArmorSummary(ArmorCandidate Best, IReadOnlyList<ArmorCandidate> Candidates, int MediumDexterityCap);
public sealed record MovementInput
{
    public string ClassName { get; init; } = "";
    public int Level { get; init; } = 1;
    public int BaseSpeed { get; init; } = 30;
    // Resolved by ClassProgressionRules from the trusted native class catalog.
    public int UnarmoredMovementBonus { get; init; }
    public IReadOnlyList<string> Feats { get; init; } = [];
    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];
}
public sealed record MovementTerm(string Name, int Amount, bool Applies);
public sealed record MovementSummary(int Speed, int? ClimbSpeed, int? SwimSpeed, Calculation Calculation, IReadOnlyList<MovementTerm> Terms);
