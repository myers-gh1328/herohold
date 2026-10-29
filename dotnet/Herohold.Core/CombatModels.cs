namespace Herohold.Core;

public enum CombatPhase { Unknown, OwnTurn, BetweenTurns, ExternalTurn }
public enum CombatCost { None, Action, Attack, BonusAction, Reaction }
public enum CombatSupport { Supported, NeedsTableFact, TableManaged }
public enum CombatBoundary { OwnStart, OwnEnd, ActorStart, ActorEnd, Round, GameTime }
public enum CombatDuration { Manual, Rounds, CreatorNextStart, CreatorNextEnd, TargetNextStart, TargetNextEnd, GameSeconds, RepeatedSave }
public enum CombatTransitionKind { Enter, Resume, Leave, EndEncounter, ConfirmOwnTurn, StartTurn, EndTurn, ExternalTurn, AdvanceRound, SpendAction, Move, AddEffect, ResolveSave, AdvanceGameTime, ConfigureLayout }

public sealed record CombatActionContext
{
    public CombatAttackProfile? AttackProfile {get;init;}
    public string EncounterId {get;init;}="";
    public string TurnId {get;init;}="";
    public string ActionId {get;init;}="";
    public string CastId {get;init;}="";
    public bool TriggerConfirmed {get;init;}
    public List<string> ConfirmedFacts {get;init;}=[];
    public List<string> ArmedEffects {get;init;}=[];
}
public sealed record CombatAttackProfile
{
    public string Name {get;init;}="";
    public DiceFormula? Dice {get;init;}
    public int Modifier {get;init;}
    public int? FixedDamage {get;init;}
    public string DamageType {get;init;}="";
}

public sealed record CombatLayout
{
    public int Version {get;init;}=1;
    public List<string> Sections {get;init;}=["attacks","spells","skills","saves","effects"];
    public List<string> HiddenSections {get;init;}=[];
    public List<string> PinnedSkills {get;init;}=["Perception","Stealth","Insight"];
    public List<string> PinnedSpells {get;init;}=[];
    public List<string> PinnedFeatures {get;init;}=[];
    public List<string> PinnedItems {get;init;}=[];
}

public sealed record CombatEffectTimer
{
    public string EffectId {get;init;}="";
    public string CreatorId {get;init;}="";
    public string TargetId {get;init;}="";
    public CombatDuration Duration {get;init;}
    public string OriginalUnit {get;init;}="manual";
    public int OriginalAmount {get;init;}
    public int Remaining {get;init;}
    public long CreatedSequence {get;init;}
    public bool AwaitingSave {get;init;}
    public string SavePrompt {get;init;}="";
}

public sealed record CombatActionGrant
{
    public string Id {get;init;}="";
    public string Source {get;init;}="";
    public int Remaining {get;init;}
    public List<string> PermittedActions {get;init;}=[];
}

public sealed record CombatAttackSelection
{
    public string CastId {get;init;}="";
    public CombatAttackProfile? AttackProfile {get;init;}
    public string RollId {get;init;}="";
    public string Target {get;init;}="";
    public string MarkEffectId {get;init;}="";
    public List<string> ArmedEffects {get;init;}=[];
}

public sealed record CombatState
{
    public int Version {get;init;}=1;
    public bool Enabled {get;init;}
    public bool EncounterActive {get;init;}
    public string EncounterId {get;init;}="";
    public int Round {get;init;}=1;
    public int OwnTurnNumber {get;init;}
    public long Sequence {get;init;}
    public CombatPhase Phase {get;init;}
    public string TurnId {get;init;}="";
    public string ActorId {get;init;}="";
    public string PendingOwnTurnBoundaryId {get;init;}="";
    public List<string> CompletedBoundaryIds {get;init;}=[];
    public bool ActionAvailable {get;init;}
    public bool BonusActionAvailable {get;init;}
    public bool ReactionAvailable {get;init;}=true;
    public int AttacksRemaining {get;init;}
    public int MovementSpent {get;init;}
    public int MovementGranted {get;init;}
    public bool SlotCastThisTurn {get;init;}
    public DeathSaveState? DeathSavesAtTurnStart {get;init;}
    public string DeathSaveRecordedTurnId {get;init;}="";
    public List<CombatActionGrant> AdditionalActions {get;init;}=[];
    public List<CombatEffectTimer> Timers {get;init;}=[];
    public CombatAttackSelection? AttackSelection {get;init;}
    public CombatLayout Layout {get;init;}=new();
}

public sealed record CombatOption(string Id,string Name,CombatCost Cost,bool Allowed,CombatSupport Support,string Reason,string Source="2024 Basic Rules");
public sealed record CombatAdvancePreview(List<string> UnusedOpportunities,List<string> ExpiringEffects,List<string> DueSaves,int RoundsSkipped)
{
    public bool RequiresConfirmation=>UnusedOpportunities.Count>0||RoundsSkipped>0;
}

public sealed record CombatTransitionRequest:PlayRequest
{
    public CombatTransitionKind Kind {get;init;}
    public bool NewEncounter {get;init;}
    public bool OwnTurnNow {get;init;}
    public bool TableBoundaryConfirmed {get;init;}
    public bool AdvanceConfirmed {get;init;}
    public bool AbandonPending {get;init;}
    public string EncounterId {get;init;}="";
    public string BoundaryId {get;init;}="";
    public string ActorId {get;init;}="";
    public string ActionId {get;init;}="";
    public int Amount {get;init;}
    public int Round {get;init;}=1;
    public ActiveEffect? Effect {get;init;}
    public CombatEffectTimer? Timer {get;init;}
    public string EffectId {get;init;}="";
    public bool SaveSucceeded {get;init;}
    public CombatLayout? Layout {get;init;}
}
