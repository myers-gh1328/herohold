using System.Text.Json.Nodes;

namespace Herohold.Core;

public enum Ability { Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma }

public sealed record CalculationTerm(string Label, int Value);
public sealed record Calculation(int Total, IReadOnlyList<CalculationTerm> Terms);

public sealed record EquipmentItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Quantity { get; init; } = 1;
    public bool Equipped { get; init; }
    public bool RequiresAttunement { get; init; }
    public bool Attuned { get; init; }
    public bool Carried { get; init; } = true;
    public decimal? UnitWeightLb { get; init; }
    public ItemCharges? Charges { get; init; }
    public ItemUseAction UseAction { get; init; }
    public bool Consumable { get; init; }
    /// <summary>The catalog weapon this item attacks as, when its own name is not one (a named or magic weapon).</summary>
    public string BaseWeapon { get; init; } = string.Empty;
    /// <summary>The catalog item this is a copy of: an official item or one of the owner's homebrew items. Empty until the store links it.</summary>
    public string CatalogItemId { get; init; } = string.Empty;
    /// <summary>The spell written on a spell scroll.</summary>
    public string ScrollSpell { get; init; } = string.Empty;
    /// <summary>Hit points regained when used, as dice plus a bonus (a Potion of Healing is 2d4 + 2); null when it does not heal.</summary>
    public DiceFormula? HealingDice { get; init; }
    public int HealingBonus { get; init; }
    public int AttackBonus { get; init; }
    public int DamageBonus { get; init; }
    public Dictionary<Ability, int> ModifierBonuses { get; init; } = [];
    public List<DamageDefense> Defenses { get; init; } = [];
    public List<ActiveEffect> PassiveEffects { get; init; } = [];
    public List<ActiveEffect> UseEffects { get; init; } = [];
}

public sealed record ResourcePool
{
    public string? SourceId { get; init; }
    public string SourceKind { get; init; } = "manual";
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Remaining { get; init; }
    public int Maximum { get; init; }
    public RecoveryRest Recovery { get; init; } = RecoveryRest.Long;
    public int? SlotLevel { get; init; }
    public int? ShortRestRecoveryAmount { get; init; }
}

public sealed record JournalNote
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ActiveEffect
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public int? RemainingRounds { get; init; }
    public bool Concentration { get; init; }
}

public sealed record CharacterState
{
    public CombatState Combat { get; init; } = new();
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Species { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string Subclass { get; init; } = string.Empty;
    public int Level { get; init; } = 1;
    public int CurrentHp { get; init; }
    public int MaximumHp { get; init; } = 1;
    public int TemporaryHp { get; init; }
    public int ArmorClass { get; init; } = 10;
    public int Speed { get; init; } = 30;
    public int Turn { get; init; } = 1;
    public int Exhaustion { get; init; }
    public Dictionary<Ability, int> AbilityScores { get; init; } = Enum.GetValues<Ability>().ToDictionary(x => x, _ => 10);
    public List<Ability> SaveProficiencies { get; init; } = [];
    public List<string> SkillProficiencies { get; init; } = [];
    public List<string> Expertise { get; init; } = [];
    public List<EquipmentItem> Equipment { get; init; } = [];
    public List<ResourcePool> Resources { get; init; } = [];
    public List<JournalNote> Notes { get; init; } = [];
    public List<ActiveEffect> Effects { get; init; } = [];
    public PlayState Play { get; init; } = new();
    public CharacterSheetState Sheet { get; init; } = new();
    public Dictionary<string, int> StandingAdjustments { get; init; } = [];
    public Dictionary<string, List<CalculationTerm>> StandingAdjustmentTerms { get; init; } = [];
    /// <summary>The player's build choices; null for a hand-made sheet with no build for the rules.</summary>
    public BuildFacts? Build { get; init; }
    /// <summary>What the rules worked out from the build; recalculated on load, never stored.</summary>
    public DerivedSheet Derived { get; init; } = new();
    public List<string> Features { get; init; } = [];
    public List<DamageDefense> Defenses { get; init; } = [];
    public KnownTargetState KnownTarget { get; init; } = new();
}

public sealed record WorkspaceSnapshot
{
    public int SchemaVersion { get; init; } = 3;
    public long Revision { get; init; }
    public CharacterState Character { get; init; } = new();
}
