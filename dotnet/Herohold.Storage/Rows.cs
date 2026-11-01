using Herohold.Core;

namespace Herohold.Storage;

// Table rows. Child rows belong to one character and are replaced together on each save.
// "Slot" 0 is the live play state; undo entry n keeps its before/after states in slots 2n+1 and 2n+2.

public sealed class UserRow
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CharacterRow
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string RulesSource { get; set; } = CharacterRecord.DefaultRulesSource;
    public long Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Build
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Subclass { get; set; } = "";
    public int Level { get; set; }
    public string Species { get; set; } = "";
    public string Background { get; set; } = "";
    public string? AbilityScoreMethod { get; set; }
    public int? BaseStrength { get; set; }
    public int? BaseDexterity { get; set; }
    public int? BaseConstitution { get; set; }
    public int? BaseIntelligence { get; set; }
    public int? BaseWisdom { get; set; }
    public int? BaseCharisma { get; set; }
    public bool? EquipmentMigrationPending { get; set; }
    public bool HasPresentation { get; set; }
    public string PrimaryWeapon { get; set; } = "";
    public string FeaturedCantrip { get; set; } = "";
    public int? ImportMaximumHpAdjustment { get; set; }
    public bool HasImportProvenance { get; set; }
    public string? ImportSourceKind { get; set; }
    public string? ImportStatus { get; set; }
    public string? ImportedAt { get; set; }
    public bool? ImportAbilityHistoryReconstructed { get; set; }
    public bool? ImportHpHistoryReconstructed { get; set; }
    public bool HasSpellPicks { get; set; }
    public int? ClassTracksVersion { get; set; }
    public string? InitialClassTrackId { get; set; }
    public int? JournalVersion { get; set; }
    public int? CustomJournalVersion { get; set; }
    public bool? JournalCleanupV1 { get; set; }
    public bool HasManualFacts { get; set; }

    // Live play facts
    public int CurrentHp { get; set; }
    public int TemporaryHp { get; set; }
    public int Turn { get; set; }
    public int Exhaustion { get; set; }
    public bool DreadUsedThisTurn { get; set; }
    public string HunterTarget { get; set; } = "";
    public string KnownTargetName { get; set; } = "";
    public string KnownTargetKnowledge { get; set; } = "";

    // Sheet
    public int? ExperiencePoints { get; set; }
    public bool? HeroicInspiration { get; set; }
    public bool HasCoins { get; set; }
    public int CoinsCp { get; set; }
    public int CoinsSp { get; set; }
    public int CoinsEp { get; set; }
    public int CoinsGp { get; set; }
    public int CoinsPp { get; set; }
    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }
    public bool Stable { get; set; }
    public bool Dead { get; set; }
    public decimal? CapacityOverrideLb { get; set; }
    public string CapacitySource { get; set; } = "";
    public bool CarryCoins { get; set; }
    public bool CountAmmunition { get; set; }
    public string? ProfileBackground { get; set; }
    public string? ProfileSize { get; set; }
    public string? Appearance { get; set; }
    public string? Backstory { get; set; }
    public string? Personality { get; set; }
    public string? Alignment { get; set; }
    public string? PlayerName { get; set; }
    public string? Age { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }
    public string? Eyes { get; set; }
    public string? Skin { get; set; }
    public string? Hair { get; set; }
    public string? Ideals { get; set; }
    public string? Bonds { get; set; }
    public string? Flaws { get; set; }
    public string? Allies { get; set; }
    public string? Organizations { get; set; }
    public string? Treasure { get; set; }
}

/// <summary>One entry of a list of names or numbers. Position -1 marks a present but empty list.</summary>
public sealed class ListValueRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public string Owner { get; set; } = "";
    public string List { get; set; } = "";
    public int Position { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public int? Number { get; set; }
}

public enum ChoiceKind { Null, Text, Number, Flag, List, Fields }

/// <summary>One node of a catalog-defined choice. Path is a JSON Pointer from the owner's choice map.</summary>
public sealed class ChoiceRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Path { get; set; } = "";
    public int Position { get; set; }
    public ChoiceKind Kind { get; set; }
    public string? Text { get; set; }
    public int? Number { get; set; }
    public bool? Flag { get; set; }
}

public sealed class FeatRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string InstanceId { get; set; } = "";
    public string FeatId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool? Repeatable { get; set; }
    public string SourceType { get; set; } = "";
    public string? SourceClassName { get; set; }
    public int? SourceLevel { get; set; }
    public string? SourceTransactionId { get; set; }
    public int? LevelTaken { get; set; }
    public bool? AbilityIncreaseInBase { get; set; }
}

public sealed class AbilityAdjustmentRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string Type { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string? SourceId { get; set; }
    public string? InstanceId { get; set; }
    public string? SourceFeat { get; set; }
    public string? SourceName { get; set; }
    public string? SourceClass { get; set; }
    public int? SourceLevel { get; set; }
    public bool HasSource { get; set; }
    public string? SourceRefType { get; set; }
    public string? SourceRefClassName { get; set; }
    public int? SourceRefLevel { get; set; }
    public string? SourceRefTransactionId { get; set; }
    public string? LevelUpChoiceId { get; set; }
    public string? FeatInstanceId { get; set; }
    public int? Maximum { get; set; }
}

public sealed class FeatureConfigurationRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string FeatureId { get; set; } = "";
    public string CurrentValue { get; set; } = "";
}

public sealed class HpGainRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public int CharacterLevel { get; set; }
    public string ClassName { get; set; } = "";
    public string Method { get; set; } = "";
    public int BaseGain { get; set; }
}

public sealed class HpRollRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public int Level { get; set; }
    public int Value { get; set; }
    public string? Mode { get; set; }
}

public sealed class LevelUpRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string ClassName { get; set; } = "";
    public int FromLevel { get; set; }
    public int ToLevel { get; set; }
    public string TransactionId { get; set; } = "";
    public int HpIncrease { get; set; }
    public string? Source { get; set; }
}

public sealed class ResolvedChoiceRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int LevelUpPosition { get; set; }
    public int Position { get; set; }
    public string ChoiceId { get; set; } = "";
}

public sealed class ClassTrackRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string TrackId { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public string? RulesSource { get; set; }
    public string? Subclass { get; set; }
}

/// <summary>One item a character owns: which catalog item, and this character's own state.</summary>
public sealed class ItemRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string ItemId { get; set; } = "";
    public string CatalogItemId { get; set; } = "";
    public int Quantity { get; set; }
    public bool Equipped { get; set; }
    public bool Attuned { get; set; }
    public bool Carried { get; set; }
    public int ChargeRemaining { get; set; }
    public string ScrollSpell { get; set; } = "";
}

/// <summary>One item definition: official (from the rules data, OwnerId null) or homebrew (OwnerId set).</summary>
public sealed class CatalogItemRow
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool RequiresAttunement { get; set; }
    public decimal? UnitWeightLb { get; set; }
    public bool HasCharges { get; set; }
    public int ChargeUseCost { get; set; }
    public int ChargeMaximum { get; set; }
    public string ChargeRecoveryNote { get; set; } = "";
    public string UseAction { get; set; } = "";
    public bool Consumable { get; set; }
    public string BaseWeapon { get; set; } = "";
    public int AttackBonus { get; set; }
    public int DamageBonus { get; set; }
    public int? HealingDiceCount { get; set; }
    public int? HealingDiceSides { get; set; }
    public int HealingBonus { get; set; }
}

public sealed class ItemBonusRow
{
    public long Id { get; set; }
    public string ItemId { get; set; } = "";
    public int Position { get; set; }
    public string Ability { get; set; } = "";
    public int Value { get; set; }
}

public sealed class ItemDefenseRow
{
    public long Id { get; set; }
    public string ItemId { get; set; } = "";
    public int Position { get; set; }
    public string Kind { get; set; } = "";
    public string Type { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>An effect a homebrew item gives while active ("passive") or when used ("use").</summary>
public sealed class ItemEffectRow
{
    public long Id { get; set; }
    public string ItemId { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Position { get; set; }
    public string EffectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    public int? RemainingRounds { get; set; }
    public bool Concentration { get; set; }
}

public sealed class DefenseRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Position { get; set; }
    public string Kind { get; set; } = "";
    public string Type { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class EffectRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public string Owner { get; set; } = "";
    public int Position { get; set; }
    public string EffectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    public int? RemainingRounds { get; set; }
    public bool Concentration { get; set; }
}

public sealed class JournalNoteRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string NoteId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class MovementModeRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string Kind { get; set; } = "";
    public int Speed { get; set; }
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class CompanionRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string CompanionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public int ArmorClass { get; set; }
    public int MaximumHp { get; set; }
    public int CurrentHp { get; set; }
    public int TemporaryHp { get; set; }
    public int Speed { get; set; }
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class CompanionActionRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int CompanionPosition { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public string Rules { get; set; } = "";
}

public sealed class ResourceRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public string Owner { get; set; } = "";
    public int Position { get; set; }
    public string PoolId { get; set; } = "";
    public string? SourceId { get; set; }
    public string SourceKind { get; set; } = "";
    public string Name { get; set; } = "";
    public int Remaining { get; set; }
    public int Maximum { get; set; }
    public string Recovery { get; set; } = "";
    public int? SlotLevel { get; set; }
    public int? ShortRestRecoveryAmount { get; set; }
}

public sealed class HitDiePoolRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Position { get; set; }
    public string PoolId { get; set; } = "";
    public int Sides { get; set; }
    public int Maximum { get; set; }
    public int Remaining { get; set; }
}

public sealed class SpellSourceRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Position { get; set; }
    public string SourceId { get; set; } = "";
    public string? ClassTrackId { get; set; }
    public string SourceKind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ability { get; set; } = "";
    public int? MaximumSpellLevel { get; set; }
    public bool CanChangeCantripsOnLongRest { get; set; }
    public int CantripReplacementLimit { get; set; }
    public bool CanChangeOnLongRest { get; set; }
    public int PreparedLimit { get; set; }
    public int? ReplacementLimit { get; set; }
    public bool FromSpellbook { get; set; }
}

/// <summary>Combat state of one slot, with the optional attack selection and turn-start death saves flattened in.</summary>
public sealed class CombatRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Version { get; set; }
    public bool Enabled { get; set; }
    public bool EncounterActive { get; set; }
    public string EncounterId { get; set; } = "";
    public int Round { get; set; }
    public int OwnTurnNumber { get; set; }
    public long Sequence { get; set; }
    public string Phase { get; set; } = "";
    public string TurnId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string PendingOwnTurnBoundaryId { get; set; } = "";
    public bool ActionAvailable { get; set; }
    public bool BonusActionAvailable { get; set; }
    public bool ReactionAvailable { get; set; }
    public int AttacksRemaining { get; set; }
    public int MovementSpent { get; set; }
    public int MovementGranted { get; set; }
    public bool SlotCastThisTurn { get; set; }
    public bool HasDeathSavesAtTurnStart { get; set; }
    public int TurnStartSuccesses { get; set; }
    public int TurnStartFailures { get; set; }
    public bool TurnStartStable { get; set; }
    public bool TurnStartDead { get; set; }
    public string DeathSaveRecordedTurnId { get; set; } = "";
    public int LayoutVersion { get; set; }
    public bool HasAttackSelection { get; set; }
    public string SelectionCastId { get; set; } = "";
    public string SelectionRollId { get; set; } = "";
    public string SelectionTarget { get; set; } = "";
    public string SelectionMarkEffectId { get; set; } = "";
    public bool HasSelectionProfile { get; set; }
    public string SelectionProfileName { get; set; } = "";
    public int? SelectionDiceCount { get; set; }
    public int? SelectionDiceSides { get; set; }
    public int SelectionModifier { get; set; }
    public int? SelectionFixedDamage { get; set; }
    public string SelectionDamageType { get; set; } = "";
}

public sealed class CombatTimerRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Position { get; set; }
    public string EffectId { get; set; } = "";
    public string CreatorId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Duration { get; set; } = "";
    public string OriginalUnit { get; set; } = "";
    public int OriginalAmount { get; set; }
    public int Remaining { get; set; }
    public long CreatedSequence { get; set; }
    public bool AwaitingSave { get; set; }
    public string SavePrompt { get; set; } = "";
}

public sealed class CombatGrantRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Position { get; set; }
    public string GrantId { get; set; } = "";
    public string Source { get; set; } = "";
    public int Remaining { get; set; }
}

public sealed class RollRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public string RollId { get; set; } = "";
    public string AttackKind { get; set; } = "";
    public int? BonusDieSides { get; set; }
    public string? InspirationWindowId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Advantage { get; set; } = "";
    public int StandingBonusTotal { get; set; }
    public int FlatBonus { get; set; }
    public int ChosenD20 { get; set; }
    public int Total { get; set; }
    public bool LuckyUsed { get; set; }
    public bool Resolved { get; set; }
    public bool? Hit { get; set; }
}

public sealed class DamageTermRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public int Position { get; set; }
    public int? DieSides { get; set; }
    public string Name { get; set; } = "";
    public string DamageType { get; set; } = "";
    public int Modifier { get; set; }
    public int Total { get; set; }
}

public sealed class CastRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public string CastId { get; set; } = "";
    public int? AttacksRemaining { get; set; }
    public string? InspirationWindowId { get; set; }
    public bool Ritual { get; set; }
    public string Mode { get; set; } = "";
    public string Target { get; set; } = "";
    public string PaymentResourceId { get; set; } = "";
    public int? EffectTotal { get; set; }
    public string? PreviewAttackRollKind { get; set; }
    public int? PreviewAttackRollCount { get; set; }
    public bool HasCastingModifierCalculation { get; set; }
    public int CastingModifierTotal { get; set; }
    public bool HasSaveDcCalculation { get; set; }
    public int SaveDcTotal { get; set; }
    public string SpellId { get; set; } = "";
    public string SpellName { get; set; } = "";
    public int SlotLevel { get; set; }
    public string EffectKind { get; set; } = "";
    public int? DiceCount { get; set; }
    public int? DiceSides { get; set; }
    public int Modifier { get; set; }
    public string FormulaText { get; set; } = "";
    public string? DamageType { get; set; }
    public int? RadiusFt { get; set; }
    public string? SaveAbility { get; set; }
    public int? SaveDc { get; set; }
    public bool Concentration { get; set; }
    public string DurationText { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Modeled { get; set; }
}

/// <summary>The tracked play state saved before or after one undoable action (slots 1 and up).</summary>
public sealed class PlaySnapshotRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Slot { get; set; }
    public bool ReceiptsCaptured { get; set; }
    public bool HasCombat { get; set; }
    public bool HasSpellSources { get; set; }
    public bool HasHitDiePools { get; set; }
    public bool HasDeathSaves { get; set; }
    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }
    public bool Stable { get; set; }
    public bool Dead { get; set; }
    public bool? HeroicInspiration { get; set; }
    public int CurrentHp { get; set; }
    public int TemporaryHp { get; set; }
    public int Turn { get; set; }
    public int Exhaustion { get; set; }
    public bool DreadUsedThisTurn { get; set; }
    public string HunterTarget { get; set; } = "";
}

public sealed class UndoEntryRow
{
    public long Id { get; set; }
    public string CharacterId { get; set; } = "";
    public int Position { get; set; }
    public string OperationId { get; set; } = "";
    public string Description { get; set; } = "";
    public bool HasAfter { get; set; }
}
