# Native scores and standing feat effects

Exact public DTOs are in AbilityModels.cs, namespace Herohold.Rules. Entry points:

- AbilityScoreRules.Calculate(AbilityScoreInput) -> AbilityScoreSummary
- StandingRules.Calculate(StandingInput) -> StandingSummary

No Web integration until independent fixture review. Existing Core/HP/progression APIs unchanged.

Scores: missing base abilities default10, explicit bases1..30. Background weighted primary/secondary selections must be distinct and within AllowedAbilities; null selections produce no bonus. Actual background bonus respects cap20 without reducing an already greater manual score. PrimaryBonus0..2, SecondaryBonus0..1. Ordered explicit ScoreAdjustment records require unique nonempty SourceId (max200chars), SourceName max200, at least one valid ability raise1..10, Maximum1..30. Adjustments apply min(requested,max(0,maximum-before)), so caps never lower existing scores. ASI and half-feat source records are already selected/validated upstream; no automatic ability increases are inferred from Feats. Barbarian20 Strength/Constitution and Monk20 Dexterity/Wisdom append native+4/max25 once; callers must not supply these automatic class sources. Maximums reports class-supported manual/ASI maxima20/25, not a ceiling for explicitly sourced epic-boon caps30. Calculations show actual applied contributions, not unclamped requests.

Standing inputs contain all six final scores1..30 and BASE save/skill/expertise collections before general-feat grants. Feeding final canonical feat-derived skill sets as base is invalid adapter behavior: it can incorrectly upgrade Keen Mind/Observant proficiency into expertise. Equipment uses existing Core activation/quantity/attunement semantics, adds directly to modifiers once per item, and never changes scores. Unique item IDs required. No StandingAdjustments input or legacy numerical deltas.

NumericFeatSelection fields represent Skill Expert, Keen Mind, Observant, Resilient and Boon of Skill. Resolve selections in given order, deduplicate grants, and require expertise to be proficient after all grants. All skill names come from the standard18; reject unknown names and undefined enum values. Duplicate modeled nonrepeatable feat names reject; repeated unmodeled names stay descriptive and deduplicate in the report. Other feat names remain explicitly UnmodeledStandingFeats; their descriptions never execute. Selected feat instances grant proficiencies only; score raises must already exist in AbilityScoreInput.Adjustments.

Native sourced standing additions: Alert origin +PB initiative; existing Ranger/Gloom>=3 Wisdom initiative; Bard>=2 Jack of All Trades floor(PB/2) on nonproficient skill checks only (2024 normalized description); Cleric Thaumaturge Wisdom modifier(min1) on Arcana/Religion; Druid Magician same on Arcana/Nature. Order choices are explicit typed enum fields, valid only for corresponding class. Jack/order bonuses are deliberate corrections to missing legacy engine numerical behavior, sourced from local XPHB class records, not parsed dynamically from text.

Bounds: class/subclass strings<=200; level1..20; score sources<=100; equipment<=500; feat selections<=100; origin feat names<=100; collection fields nonnull. Input state is never mutated; outputs are detached/read-only. Unknown class identifiers have no automatic class effects. No creation/level-up eligibility, spell grants, HP, armor, movement, attack triggers or arbitrary textual effects in this module.

## Canonical source adapter

CharacterBuildCatalog.Parse(ClassProgressionCatalog,backgroundsJson,speciesJson) parses trusted bounded records once. Derive(JsonObject,IReadOnlyList<EquipmentItem>,allowUnresolved=false) returns NativeBuildSummary. NativeBuildService.Normalize is a detached Web adapter; root controls its live integration.

Fresh pre-feat sources come from trusted background/class/species choices, class-choice thresholds and selected origin instances. Canonical feat_instance_id identifies repeatable score sources. Incomplete weighted-background choices remain editable with Issues; completed invalid choices reject. Old canonical grants not explained by reconstructed sources are preserved as manual/unattributed grants with Issues. folio_native_build_sources stores bounded manual skills/expertise/saves plus derivedSkills/derivedExpertise/derivedSaves ownership snapshots so removing a known source does not turn its stale legacy grant into a manual grant. These are standard18skill/6ability arrays only. PDF review expertise is explicitly retained.

folio_native_standing_terms contains named Jack/order/Alert differential terms for checked Core display. Native weapon metadata supplies explicit abilityCandidates; choose highest final modifier, preserving selected ability on ties. Preserve only actual weapon-specific attack/damage differences measured before replacing the old governing modifier.

DependentResourceRules.Calculate uses named reviewed source formulas for Bard, Ranger, Cleric, Wizard, Glory and Psi Warrior capacities plus casting/maneuver/Shield Master DCs. It changes recognized capacities, not remaining uses; projection preserves existing spent amounts. Unknown resource records retain their existing manual/unported policy. Every spellcasting source's DC/attack uses its own declared ability. No arbitrary formula or effect-text parsing.
