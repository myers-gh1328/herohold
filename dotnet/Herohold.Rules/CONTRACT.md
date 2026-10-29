# Native hit points and class progression — proposed public contract

Namespace Herohold.Rules, net10.0, references Herohold.Core only. No Web reference or
live application cutover before root acceptance and reviewed comparison fixtures.

## Hit points

- enum HitPointMethod { Maximum, Fixed, Rolled }.
- record HitPointHistoryEntry(int CharacterLevel,string ClassName,HitPointMethod Method,int BaseGain).
- HitPointInput init-settable record:
  string ClassName; int Level,HitDie,ConstitutionModifier;
  IReadOnlyList<HitPointHistoryEntry> History (default empty);
  bool Tough,DwarvenToughness;
  int AdditionalMaximumBonus,ImportedMaximumAdjustment,CurrentHp,TemporaryHp.
- HitPointContribution(int CharacterLevel,HitPointMethod Method,int BaseGain,
  int ConstitutionModifier,int GainAfterMinimum,int ToughBonus,int SpeciesBonus,int Total).
- HitPointSummary record with Maximum,CurrentHp,TemporaryHp,
  CalculatedBeforeImportAdjustment,ImportAdjustment,
  IReadOnlyList<HitPointHistoryEntry> History,
  IReadOnlyList<HitPointContribution> Contributions.
- static HitPointRules.FixedHitDieGain(int hitDie) : int.
- static HitPointRules.Calculate(HitPointInput input) : HitPointSummary.
- static HitPointRules.FromCanonical(JsonObject character,int hitDie,
  int constitutionModifier,bool tough=false,bool dwarvenToughness=false,
  int additionalMaximumBonus=0) : HitPointInput.

Inputs are detached. Supported levels1–20 and dice4/6/8/10/12. A single class is
supported; duplicate levels, foreign class entries, unsupported methods and
out-of-range base rolls reject visibly. Missing levels are synthesized as a
maximum die at1 and fixed die thereafter. Explicit rolled bases after level1 remain. Level1 requires Maximum and the full class Hit Die; imported maximum adjustments remain separate.
Constitution is applied using the current modifier at every historical level,
with minimum1 per level before Tough(+2/level) and Dwarven Toughness(+1/level).
AdditionalMaximumBonus represents explicitly resolved sources such as Draconic
Resilience or Boon of Fortitude; this helper does not infer feat/subclass choices.
The imported adjustment is added once after the calculated maximum and clamped
to minimum1. Current HP is clamped downward only and temporary HP is unchanged.
Creation's initial full HP or explicit level-up healing is a separate policy.

FromCanonical reads name/class/level, hp_history, hit_points.current/temporary,
and import_overrides.maximum_hp_adjustment without changing the JsonObject.
Caller supplies the authoritative hit die, Constitution modifier and feature
eligibility; it must not infer a feature merely from a description match.

## Class progression

- ClassIdentity record: Name,Source,HitDie,IReadOnlyList<string> Subclasses.
- ClassProgressionCatalog.Parse(string normalizedClassesJson) : catalog.
- catalog.Classes : IReadOnlyList<ClassIdentity>.
- static ClassProgressionRules.AtLevel(ClassProgressionCatalog catalog,
  string className,int level,string subclassName="") : ClassProgressionSnapshot.
- ClassProgressionSnapshot init-settable record:
  string ClassName,Source,SubclassName; int Level,HitDie;
  IReadOnlyList<string> FeaturesAtLevel,FeaturesUpToLevel,
  SubclassFeaturesAtLevel,SubclassFeaturesUpToLevel;
  JsonArray ChoicesAtLevel; JsonObject Resources;
  SpellcastingProfile? Spellcasting; SubclassGrantSummary SubclassGrants.
- SpellcastingProfile init-settable record:
  Ability Ability; string SpellList,CastingType;
  IReadOnlyDictionary<int,int> Slots;
  IReadOnlyList<int> AvailableSpellLevels;
  int PreparedLimit,KnownLimit,SpellbookLimit,CantripLimit;
  IReadOnlyList<string> GrantedCantrips.
- SubclassGrantSummary init-settable record:
  IReadOnlyList<string> AlwaysPreparedSpells,AlwaysKnownSpells,GrantedCantrips;
  JsonObject Resources; JsonArray SpellGrants.

Catalog input is the existing normalized classes.json array. Parse once, validate
bounds/duplicate class names, keep private detached records, and expose snapshots
with no mutable aliases into the catalog. No filesystem access in the module.
Root controls the trusted source file/configuration. Unknown sources/properties
are retained internally rather than emitted as unsupported claims.

Resources mirror get_class_resources: level_1_rules plus the selected progression
entry's resources and slots. Heterogeneous numbers/strings/bools/objects are kept
as detached JSON. Spellcasting separately reflects effective_spellcasting,
including Wizard spellbook size, pact available-level semantics, and subclass
third-caster profiles. Subclass spellcasting is inactive below the first explicit
spellcasting_progression threshold, even when a static fallback is present.
Subclass grants accumulate in ascending threshold order; resources override by
later source level, spell names/records merge with existing stable semantics.

## Comparison boundaries

Python reference: rules/hit_points.py and rules/leveling.py, plus the existing
canonical import adjustment in services/character_engine.py.
Positive-CON ordinary cases must match. Independently reviewed corrections are
explicit fixtures: per-level minimum1 (legacy max helper applies minimum only
after summing) and no early third-caster slots below an explicit subclass threshold.
Do not change expected answers to match legacy defects. Root/parity agent approves
fixtures and decides any source-engine fixes separately.

## Live adapter boundary

`FromResolvedCanonical` consumes resolved Tough origin identities across character, background and species origin selections; Dwarven Toughness from species_traits; selected Draconic Sorcery at Sorcerer3+; and general-feat Boon of Fortitude. Source resolution remains outside the numerical rule.

Subclass identity may be queried before selection, but subclass features, grants and casting remain inactive until the class progression first offers subclass_choice.

The Web singleton NativeRulesService replaces HP, class slot capacities and preparation limits before displaying or storing bridge results. C# final Constitution includes active item modifier bonuses once. Existing current/temporary HP survive recalculation; explicit level-up HP gain compares native source and candidate maxima. Audit gains update without rewriting unmodified recorded rolls. Imports carry bounded observed current/temporary HP across the compatibility recalculation and clamp only against the final native maximum. Export uses a narrow nativeHp override before JSON/PDF bytes are generated. No arbitrary derived field overrides are accepted.
