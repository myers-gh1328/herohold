# Native play contract

All declarations are in PlayModels.cs, namespace Herohold.Core. Existing domain
models retain properties. CharacterState adds Play (PlayState), Build
(BuildFacts?, the player's typed build choices; null for a hand-made sheet), Derived
(DerivedSheet, the rules' sheet output, recalculated on load and never stored),
Features (List<string>), Defenses (List<DamageDefense>). ResourcePool adds Recovery (RecoveryRest, default Long).
Workspace schema3 reads schemas1 and2 as detached defaults without rewriting their files. Schema3 adds checked named standing-adjustment terms; subsequent explicit saves write schema3.

Public entry points:

- SpellCatalog.All : IReadOnlyList<SpellDefinition>; Find(CharacterState,string).
- SpellRules.Preview(CharacterState,string spellId,int slotLevel) : CastingPreview.
- RestRules.Preview(WorkspaceSnapshot,RestRequest,Func<int,int>? dice=null) : RestPreview.
- PlayRules.Roll(WorkspaceSnapshot,RollRequest,Func<int,int>? dice=null) : CharacterState.
- PlayRules.UseLucky(WorkspaceSnapshot,LuckyRequest,Func<int,int>? dice=null) : CharacterState.
- PlayRules.ResolveAttack(WorkspaceSnapshot,AttackResolutionRequest,Func<int,int>? dice=null) : CharacterState.
- PlayRules.Cast(WorkspaceSnapshot,CastRequest) : CharacterState.
- PlayRules.ResolveCastEffect(WorkspaceSnapshot,CastEffectRequest,Func<int,int>? dice=null) : CharacterState.
- PlayRules.Rest(WorkspaceSnapshot,RestRequest,Func<int,int>? dice=null) : CharacterState.
- PlayRules.Undo(WorkspaceSnapshot,UndoRequest) : CharacterState.

Requests have stable OperationId and SourceRevision. The server passes current
snapshot; duplicate IDs return a detached unchanged state before revision checks.
New stale IDs fail with WorkspaceConflictException. Root session persists returned
character with existing repository revision guard. Never regenerate an operation
ID during a retry. All physical dice are explicit bounded lists; no hidden RNG.

PlayState.LastRoll/LastCast expose recorded results. Resolved attack is final;
Lucky requires an unresolved matching roll. Known Advantage prevents extra Lucky
spending; Disadvantage + Lucky cancels using the first original d20. Cast effects
resolve once; Hunter's Mark effects occur on confirmed marked attack hits.

Spell metadata drives preview and effect rolls. Unmodeled spells remain readable,
permit explicit eligible slot spending, and give no invented numeric result.
Prepared/granted eligibility is checked. Free Hunter's Mark uses its base level.
Concentration is replaced only on confirmed cast. Target HP is never auto-mutated.

PreparationPolicy supplied from class build has EligibleSpellIds, PreparedLimit,
ReplacementLimit (null = unrestricted within quota), FromSpellbook, and explicit
CanChangeOnLongRest. No spell changes without RestRequest.ChangeSpells. Short rest
spends requested HD only, heals with CON once/die, and recovers Short resources.
Long rest restores HP/allHD plus Long/Short pools. EndConcentration is explicit.
Preview is detached; callers use the staged physical/digital dice for the same
commit (put preview.HitDice in PhysicalHitDice with Mode Physical) to avoid reroll.

UndoHistory is bounded and persisted; resource snapshots omit notes, equipment,
identity and build payload. Undo invalidates last cast/roll, preventing free Lucky
results after refund. A fresh library/edit/level-up adoption should clear undo
history when it changes resource maxima or character build.

## Follow-up additions

- ResourcePool.SlotLevel optionally identifies pact/other named slot pools; slot1
  through slot9 IDs infer level when unset. ShortRestRecoveryAmount optionally
  restores a fixed number (for example Second Wind/Channel Divinity); null on a
  Short pool means full recovery. Long rests fully recover Short and Long pools.
- PlayRules.AdjustResource and AdjustHp accept ResourceAdjustmentRequest and
  HpAdjustmentRequest, record bounded undo, and use the same operation/revision
  guards. ResolveRoll(snapshot,ResolveRollRequest) finalizes a non-attack roll.
- CharacterState.StandingAdjustments contains explicit transitional feature
  deltas at ability:<Ability>, save:<Ability>, skill:<Name>, or initiative. Each
  delta is added once at its own calculation level with an explanation term.
- WorkspaceRules.Validate validates an import candidate without saving it.
  JsonWorkspaceRepository.ListAsync lists only its configured directory's safe-ID
  .json workspace records; malformed files surface as errors without reset.

## Source rules and safe undo

SpellDefinition.CastingAbility overrides class casting ability for DC and formula
modifiers. GrantResourceId binds a finite free-cast source; it must match the
chosen spell record and can pay only its base-level effect. AtWill permits a
granted spell at base level with no payment. AllowsSlotCasting defaults true;
false rejects spell slots while retaining that source's bound free cast. Ritual
marks a ritual-capable spell; RitualOnly requires CastRequest.Ritual. Ritual
casting has no payment and no upcast; a Wizard's book-only ritual is eligible
without normal preparation. Setup/time passage remains table-managed.

CharacterState.Exhaustion is 0–6 and is set explicitly with
WorkspaceRules.SetExhaustion. Long rests reduce it by one; short rests retain it.
Other exhaustion penalties and death resolution are explicitly table-managed.
Incapacitated, Paralyzed, Stunned, Petrified, and Unconscious end concentration
and the marked target; removing/expiring a mark also clears its target.

New PlayUndoEntry records include Before and After checkpoints. Undo applies a
selective inverse only to fields/resources/effects changed by the original
action, preserving unrelated later HP, conditions, exhaustion, notes and turns.
Overlapping later changes cause a visible conflict. Old entries without After
cannot be undone safely and refuse rollback; current state stays preserved.

## Reviewed cantrip scaling and numeric help

SpellDefinition.CharacterLevelScaling (List<SpellDiceThreshold>) models reviewed
character-level dice thresholds. Six saving-throw cantrips are represented; see
SPELL_METADATA.md for raw-data provenance and explicit automation boundaries.
CastingPreview.CastingModifierCalculation and SaveDcCalculation are populated
for new previews; nullable fields preserve old receipts without fabricated
historical explanations. UI displays those domain calculations for numeric help.

ResourcePool.SourceKind is explicit ownership metadata: manual (default), build,
or grant. Projection can replace removed build/grant pools while preserving
user-owned manual counters. Recovery remains a separate policy and is never
inferred merely from SourceKind. Checkpoints retain this field.

PlayRequest.SourceCharacterId captures the character identity when a draft/action
is opened. Nonempty IDs are checked against the current workspace before retry
or revision checks, including rest previews. New UI callers always supply it;
empty remains only for older internal callers. Reopening a different character
with the same revision number cannot authorize an old request.

JsonWorkspaceRepository.ValidateForStorage(snapshot) performs pure preflight
using the exact same detached schema/character/next-revision preparation and
serialized byte limit as SaveAsync. Use it before marking an upload ready for
adoption; per-field validation alone cannot establish aggregate capacity.

StandingAdjustmentTerms maps the same supported keys as StandingAdjustments to1–20 named CalculationTerm records. Labels are1–200characters, values±100; each list must sum exactly to its corresponding numeric adjustment. Entries without a numeric adjustment reject. Clones detach source lists. Older workspaces with no named terms retain their generic breakdown fallback.

## Sheet field remediation: spells and ordinary attacks

- RollKind.Ability and RollKind.SpellAttack are additive kinds. Ability checks use
  AbilityCheck; spell attacks use the chosen source ability and proficiency.
  D20-only Exhaustion terms do not change base ability modifiers, damage or DCs.
- RollRequest and RollOutcome carry AttackKind (Unknown, Weapon, Unarmed, Spell).
  Legacy outcomes default Unknown. Dreadful Strike requires a recorded Weapon
  source; it cannot be enabled from an unarmed, spell or unknown old receipt.
- AttackResolutionRequest.BaseFixedDamage supports ordinary unarmed damage without
  an invented die. Its fixed base and modifier do not double on a critical hit.
  Native attack metadata exposes nullable damageDice and optional fixedDamage;
  Grapple/Shove are explicit source references, not fake attack-damage automation.
- Ranger damage upgrades resolve Ranger class levels from class_levels when given,
  rather than unlocking from total multiclass level.
- SpellDefinition retains source-specific component/material requirements, cost,
  consumption, duration and rules/casting source. Trusted raw enrichment matches
  both name and edition; an older same-name spell is not given 2024 numeric rules.
- PlayState.SpellNotes stores per-owned-spell personal notes. SpellNoteRules.Set
  returns a detached candidate; session writes retain the character identity guard.
- PreparationPolicy may explicitly grant source-owned cantrip replacement on a
  Long Rest. RestRequest.ChangeCantrips defaults false, and NewClassCantripSpellIds
  changes only that source's cantrips. The native Wizard policy allows one swap
  from the Wizard list; other grants/known sources remain unchanged. It does not
  turn leveled Wizard spell preparation into learning from the entire catalog.
- RestRules.EligiblePreparations filters the Wizard's actual spellbook and supported
  level before presenting choices. Rest/undo persist both known cantrips and their
  source-owned selection. Older undo entries without those checkpoints preserve
  existing state rather than manufacturing historical ownership.

Explicit HitDiePools are the authority for remaining Hit Dice. The hitdice resource
is their aggregate projection. Both WorkspaceRules.AdjustResource and the guarded
PlayRules operation synchronize a scalar hitdice adjustment when exactly one pool
exists; multiple pools reject an ambiguous scalar adjustment. The mounted counters
show each die size and call HitDiePoolAdjustmentRequest (PoolId, Delta), through
WorkspaceSession.AdjustHitDiePoolAsync. This retains normal source/revision guards,
idempotency and undo checkpoints. Bounds are checked before mutation. A counted-down
pool cannot subsequently be spent by a Short Rest. Undo restores only affected
pools and derives the aggregate afterward, preserving unrelated pool corrections;
an older aggregate-only undo without pool provenance fails safely.
