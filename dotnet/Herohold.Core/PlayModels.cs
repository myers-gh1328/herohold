namespace Herohold.Core;

public enum DiceMode { Digital, Physical }
public enum RollAdvantage { Normal, Advantage, Disadvantage }
public enum RollKind { Attack, Save, Skill, Initiative, Ability, SpellAttack }
public enum AttackKind { Unknown, Weapon, Unarmed, Spell }
public enum RestKind { Short, Long }
public enum RecoveryRest { Manual, Short, Long }
public enum SpellEffectKind { Damage, Healing, Utility }
public enum ScalingStatus { Modeled, NoBenefit, Unmodeled }
public enum SpellAttackPattern { Unmodeled, Single, EldritchBlast, ScorchingRay }

public sealed record DiceFormula(int Count, int Sides);
public sealed record DamageDefense
{
    public string Kind { get; init; } = "resistance";
    public string Type { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}
public sealed record KnownTargetState
{
    public string Name { get; init; } = string.Empty;
    public string Knowledge { get; init; } = "unknown";
    public List<DamageDefense> Defenses { get; init; } = [];
}
public sealed record SpellDefinition
{
    public string? AttackRollKind {get;init;}
    public SpellAttackPattern AttackPattern {get;init;}
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Source { get; init; } = "2024";
    public int Level { get; init; }
    public string CastingSource {get;init;}=string.Empty;
    public string RulesSource {get;init;}=string.Empty;
    public string School {get;init;}=string.Empty;
    public List<string> Components {get;init;}=[];
    public bool ComponentsKnown {get;init;}
    public string MaterialDescription {get;init;}=string.Empty;
    public int? MaterialCostCp {get;init;}
    public bool? MaterialConsumed {get;init;}
    public bool MaterialConsumptionOptional {get;init;}
    public string DurationDescription {get;init;}=string.Empty;
    public string CastingTime { get; init; } = "Action";
    public string Range { get; init; } = string.Empty;
    public bool Modeled { get; init; }
    public SpellEffectKind EffectKind { get; init; } = SpellEffectKind.Utility;
    public DiceFormula? Dice { get; init; }
    public bool AddCastingModifier { get; init; }
    public Ability? CastingAbility { get; init; }
    public string? GrantResourceId { get; init; }
    public bool AtWill { get; init; }
    public bool AllowsSlotCasting { get; init; } = true;
    public bool RitualOnly { get; init; }
    public bool Ritual { get; init; }
    public string? DamageType { get; init; }
    public int? RadiusFt { get; init; }
    public bool Concentration { get; init; }
    public string DurationUnit { get; init; } = "instantaneous";
    public int DurationAmount { get; init; }
    public Ability? SaveAbility { get; init; }
    public string Resolution { get; init; } = "utility";
    public string Trigger { get; init; } = "on-cast";
    public ScalingStatus Scaling { get; init; } = ScalingStatus.Unmodeled;
    public int DicePerSlot { get; init; }
    public int RadiusPerSlot { get; init; }
    public List<SpellDurationThreshold> DurationThresholds { get; init; } = [];
    public List<SpellDiceThreshold> CharacterLevelScaling { get; init; } = [];
}
public sealed record SpellDurationThreshold(int MinimumSlotLevel, int Amount, string Unit);
public sealed record SpellDiceThreshold(int MinimumCharacterLevel, DiceFormula Dice);
public sealed record CastingPreview
{
    public string? AttackRollKind {get;init;}
    public int? AttackRollCount {get;init;}
    public Calculation? CastingModifierCalculation { get; init; }
    public Calculation? SaveDcCalculation { get; init; }
    public string SpellId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SlotLevel { get; init; }
    public SpellEffectKind EffectKind { get; init; }
    public DiceFormula? Dice { get; init; }
    public int Modifier { get; init; }
    public string FormulaText { get; init; } = string.Empty;
    public string? DamageType { get; init; }
    public int? RadiusFt { get; init; }
    public Ability? SaveAbility { get; init; }
    public int? SaveDc { get; init; }
    public bool Concentration { get; init; }
    public string DurationText { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string Trigger { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public bool Modeled { get; init; }
}
public sealed record PreparationPolicy
{
    public int? MaximumSpellLevel { get; init; }
    public bool CanChangeCantripsOnLongRest {get;init;}
    public int CantripReplacementLimit {get;init;}
    public List<string> OwnedCantripSpellIds {get;init;}=[];
    public List<string> EligibleCantripSpellIds {get;init;}=[];
    public bool CanChangeOnLongRest { get; init; }
    public int PreparedLimit { get; init; }
    public int? ReplacementLimit { get; init; }
    public bool FromSpellbook { get; init; }
    public List<string> EligibleSpellIds { get; init; } = [];
}
public sealed record PlayState
{
    public List<SpellSourceState> SpellSources { get; init; } = [];
    public List<HitDiePool> HitDiePools { get; init; } = [];
    public Dictionary<string,string> SpellNotes {get;init;}=new(StringComparer.Ordinal);
    public Ability SpellcastingAbility { get; init; } = Ability.Wisdom;
    public int HitDieSize { get; init; } = 10;
    public List<string> PreparedSpellIds { get; init; } = [];
    public List<string> KnownSpellIds { get; init; } = [];
    public List<string> SpellbookIds { get; init; } = [];
    public List<string> GrantedSpellIds { get; init; } = [];
    public List<SpellDefinition> Spells { get; init; } = [];
    public PreparationPolicy Preparation { get; init; } = new();
    public bool DreadUsedThisTurn { get; init; }
    public string HunterTarget { get; init; } = string.Empty;
    public RollOutcome? LastRoll { get; init; }
    public CastOutcome? LastCast { get; init; }
    public List<string> CompletedOperationIds { get; init; } = [];
    public List<PlayUndoEntry> UndoHistory { get; init; } = [];
}
public sealed record PlayTrackedState
{
    public bool ReceiptsCaptured { get; init; }
    public RollOutcome? LastRoll { get; init; }
    public CastOutcome? LastCast { get; init; }
    public CombatState? Combat { get; init; }
    public List<SpellSourceState>? SpellSources { get; init; }
    public List<HitDiePool>? HitDiePools { get; init; }
    public List<string>? KnownSpellIds {get;init;}
    public List<string>? OwnedCantripSpellIds {get;init;}
    public DeathSaveState? DeathSaves { get; init; }
    public bool? HeroicInspiration { get; init; }
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
    public int Turn { get; init; }
    public int Exhaustion { get; init; }
    public List<ResourcePool> Resources { get; init; } = [];
    public List<ActiveEffect> Effects { get; init; } = [];
    public List<string> PreparedSpellIds { get; init; } = [];
    public bool DreadUsedThisTurn { get; init; }
    public string HunterTarget { get; init; } = string.Empty;
}
public sealed record PlayUndoEntry
{
    public string OperationId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PlayTrackedState Before { get; init; } = new();
    public PlayTrackedState? After { get; init; }
}
public sealed record RollOutcome
{
    public AttackKind AttackKind {get;init;}
    public int? BonusDieSides {get;init;}
    public string? InspirationWindowId {get;init;}
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public RollKind Kind { get; init; }
    public DiceMode Mode { get; init; }
    public RollAdvantage Advantage { get; init; }
    public List<int> D20 { get; init; } = [];
    public List<int> BonusDice { get; init; } = [];
    public Calculation StandingBonus { get; init; } = new(0, []);
    public int FlatBonus { get; init; }
    public int ChosenD20 { get; init; }
    public int Total { get; init; }
    public bool LuckyUsed { get; init; }
    public bool Resolved { get; init; }
    public bool? Hit { get; init; }
    public List<DamageTerm> Damage { get; init; } = [];
}
public sealed record DamageTerm
{
    public int? DieSides {get;init;}
    public string Name { get; init; } = string.Empty;
    public string DamageType { get; init; } = string.Empty;
    public List<int> Dice { get; init; } = [];
    public int Modifier { get; init; }
    public int Total { get; init; }
}
public sealed record CastOutcome
{
    public int? AttacksRemaining {get;init;}
    public string? InspirationWindowId {get;init;}
    public bool Ritual { get; init; }
    public string Id { get; init; } = string.Empty;
    public CastingPreview Preview { get; init; } = new();
    public DiceMode Mode { get; init; }
    public string Target { get; init; } = string.Empty;
    public string PaymentResourceId { get; init; } = string.Empty;
    public List<int> EffectDice { get; init; } = [];
    public int? EffectTotal { get; init; }
}
public abstract record PlayRequest
{
    public CombatActionContext? CombatContext { get; init; }
    public string SourceCharacterId { get; init; } = string.Empty;
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public long SourceRevision { get; init; }
}
public sealed record RollRequest : PlayRequest
{
    public AttackKind AttackKind {get;init;}
    public string Name { get; init; } = "Roll";
    public RollKind Kind { get; init; }
    public Ability Ability { get; init; } = Ability.Dexterity;
    public string Skill { get; init; } = string.Empty;
    public bool Proficient { get; init; } = true;
    public int StandingFlatBonus { get; init; }
    public int FlatBonus { get; init; }
    public DiceFormula? BonusDice { get; init; }
    public DiceMode Mode { get; init; }
    public RollAdvantage Advantage { get; init; }
    public List<int> PhysicalD20 { get; init; } = [];
    public List<int> PhysicalBonusDice { get; init; } = [];
}
public sealed record LuckyRequest : PlayRequest
{
    public string RollId { get; init; } = string.Empty;
    public int? PhysicalD20 { get; init; }
}
public sealed record AttackResolutionRequest : PlayRequest
{
    public string RollId { get; init; } = string.Empty;
    public bool Hit { get; init; }
    public DiceFormula BaseDamage { get; init; } = new(1, 8);
    public int? BaseFixedDamage { get; init; }
    public int DamageModifier { get; init; }
    public string DamageType { get; init; } = "Piercing";
    public bool Critical { get; init; }
    public string Target { get; init; } = string.Empty;
    public bool HuntersMark { get; init; }
    public bool DreadfulStrike { get; init; }
    public List<int> PhysicalDamageDice { get; init; } = [];
}
public sealed record CastRequest : PlayRequest
{
    public bool Ritual { get; init; }
    public string SpellId { get; init; } = string.Empty;
    public int SlotLevel { get; init; } = 1;
    public string PaymentResourceId { get; init; } = "slot1";
    public string Target { get; init; } = string.Empty;
    public bool QualifyingHit { get; init; }
    public bool ConfirmConcentration { get; init; }
    public DiceMode Mode { get; init; }
}
public sealed record CastEffectRequest : PlayRequest
{
    public string CastId { get; init; } = string.Empty;
    public List<int> PhysicalDice { get; init; } = [];
}
public sealed record HunterMarkRetargetRequest : PlayRequest
{
    public string Target { get; init; } = string.Empty;
    public bool PreviousTargetAtZeroHp { get; init; }
}
public sealed record RestRequest : PlayRequest
{
    public List<SourcePreparationChange> SourceChanges { get; init; } = [];
    public List<HitDieSpend> HitDieSpends { get; init; } = [];
    public RestKind Kind { get; init; }
    public DiceMode Mode { get; init; }
    public int HitDiceToSpend { get; init; }
    public List<int> PhysicalHitDice { get; init; } = [];
    public bool ChangeCantrips {get;init;}
    public List<string> NewClassCantripSpellIds {get;init;}=[];
    public bool ChangeSpells { get; init; }
    public List<string> NewPreparedSpellIds { get; init; } = [];
    public bool EndConcentration { get; init; } = true;
}
public sealed record UndoRequest : PlayRequest;
public sealed record RestPreview
{
    public List<SpellSourceState> SpellSources { get; init; } = [];
    public List<HitDiePool> HitDiePools { get; init; } = [];
    public List<HitDieSpendResult> HitDieSpendResults { get; init; } = [];
    public List<string> KnownSpellIds {get;init;}=[];
    public List<string> OwnedCantripSpellIds {get;init;}=[];
    public int Exhaustion { get; init; }
    public int CurrentHp { get; init; }
    public int MaximumHp { get; init; }
    public int HitDiceSpent { get; init; }
    public List<int> HitDice { get; init; } = [];
    public List<ResourcePool> Resources { get; init; } = [];
    public List<string> PreparedSpellIds { get; init; } = [];
    public bool EndsConcentration { get; init; }
    public string Note { get; init; } = string.Empty;
}

public sealed record ResourceAdjustmentRequest : PlayRequest
{
    public string ResourceId { get; init; } = string.Empty;
    public int Delta { get; init; }
}
public sealed record HitDiePoolAdjustmentRequest : PlayRequest
{
    public string PoolId {get;init;}=string.Empty;
    public int Delta {get;init;}
}
public sealed record HpAdjustmentRequest : PlayRequest
{
    public int Delta { get; init; }
}

public sealed record ResolveRollRequest : PlayRequest
{
    public string RollId { get; init; } = string.Empty;
}
