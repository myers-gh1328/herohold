# Reviewed native spell execution metadata

Numeric effects are separate from descriptions. The native catalog models five
reviewed examples:
Hail of Thorns, Cure Wounds, Fog Cloud, Hunter's Mark, and Disguise Self. Raw text
or a missing field is never treated as proof that an effect has no scaling.

Additional common spells were checked against the same local XPHB catalog and
its complete higher-slot text. `CommonSpellTests` first failed on missing records,
then passed with the following explicit structured data:

| Spell | Base effect | Each higher slot | Resolution |
| --- | --- | --- | --- |
| Fireball | 8d6 Fire, level3,20-foot radius | +1d6 | Dexterity, half on success |
| Burning Hands | 3d6 Fire, level1,15-foot cone | +1d6 | Dexterity, half on success |
| Thunderwave | 2d8 Thunder, level1,15-foot cube | +1d8 | Constitution, half on success |
| Healing Word | 2d4 + casting modifier, level1 | +2d4 | Healing; apply to chosen target manually |

These records roll base effects; target saves, pushes, objects and other riders
remain table actions. Healing never automatically heals the caster. Physical
effect input is validated against the same preview dice and adds the selected
source's casting modifier once.

The following cantrip facts were checked directly against
`data/raw/5etools/spells/spells-xphb.json`: each spell's `scalingLevelDice`,
`damageInflict`, `savingThrow`, `range`, `duration`, and rules entries.

| Spell | Dice at character levels 1 / 5 / 11 / 17 | Type | Save |
| --- | --- | --- | --- |
| Acid Splash | 1d6 / 2d6 / 3d6 / 4d6 | Acid | Dexterity |
| Sacred Flame | 1d8 / 2d8 / 3d8 / 4d8 | Radiant | Dexterity |
| Vicious Mockery | 1d6 / 2d6 / 3d6 / 4d6 | Psychic | Wisdom |
| Mind Sliver | 1d6 / 2d6 / 3d6 / 4d6 | Psychic | Intelligence |
| Thunderclap | 1d6 / 2d6 / 3d6 / 4d6 | Thunder | Constitution |
| Word of Radiance | 1d6 / 2d6 / 3d6 / 4d6 | Radiant | Constitution |

`SpellDiceThreshold` and `SpellDefinition.CharacterLevelScaling` represent these
thresholds; preview and effect resolution use the same record. These are base
spell damage rolls. The target's save, spell riders, class/item damage bonuses,
and target HP application remain explicit table actions. The preview says so.

Spell-attack and multiple-beam cantrips, conditional formulas such as Toll the
Dead, exploding dice, and weapon-changing cantrips have not been inferred from
similar-looking prose or dice tags. They retain unmodeled execution until their
complete resolution flow is represented and verified.

The native Ranger module additionally applies the existing reviewed 2024 Foe
Slayer capstone: Hunter's Mark uses 1d10 at Ranger 20, otherwise 1d6. This is a
class-level effect, not a higher-slot damage increase. Both marked-hit resolution
and casting preview use the same rule.

New previews provide domain-owned CastingModifierCalculation and
SaveDcCalculation for numeric help. Older recorded receipts can lack these
nullable fields; do not reconstruct their historical sources from a changed
current character.

## Reference and source fields

The native catalog now enriches normalized component records from the matching
raw name/source record. Component letters, material text, copper-piece value and
consumed/optional-consumption flags are retained separately. Duration and school
remain readable reference data even when numerical execution is unmodeled.
SpellInspector presents those facts with source/access and personal spell notes;
MagicCastingSummary shows casting ability/modifier, DC and attack by source.
A source-specific spell attack is a D20 roll, not a weapon attack. DCs use the base
casting modifier and proficiency; Exhaustion applies only to the attack test.

## Finite spell attacks and per-hit damage

The raw XPHB `spellAttack` field supplies melee/ranged reference capability. It does
not establish when attacks happen or how many are allowed. Catalog enrichment
therefore records `spell_attack_kind` and an explicitly reviewed finite pattern.
Typed SpellDefinition.AttackRollKind and AttackPattern produce
CastingPreview.AttackRollKind and nullable AttackRollCount, independently of whether
damage execution is modeled. Zero means no spell attack; null means the attack
timing/count is table-managed. Persistent/conditional spells do not receive invented
free attacks from the raw flag.

Reviewed numerical execution covers Fire Bolt (one attack, damage 1/2/3/4d10 at total
levels 1/5/11/17), Eldritch Blast (1/2/3/4 separate attacks at those levels, always
1d10 per beam hit), and Scorching Ray (three rays plus one per slot level above 2,
always 2d6 per ray hit). These follow the official 2024
[spell descriptions](https://www.dndbeyond.com/sources/dnd/br-2024/spell-descriptions#EldritchBlast).
Additional source bonuses, riders and target adjustments remain explicit/table-managed.

CastOutcome.AttacksRemaining is the authoritative remaining count in ordinary and
combat play. Each bound spell-attack roll consumes one opportunity, including a
miss; it does not pay the casting resource again. Damage uses that receipt's
per-hit profile after a confirmed hit. The whole-cast effect endpoint cannot bypass
the attack/hit sequence. CombatMode does not maintain a second spell-attack budget.

Older trusted native reference records for the three reviewed spells can derive
new execution metadata through SpellCatalog.Find without rewriting the stored
definition. Existing historical casting receipts retain their original metadata.
Older editions and unknown sources do not acquire 2024 execution from a matching name.

ProductionSpellAttackTests exercise actual enriched catalog records through
projection, casting, finite physical attacks, hit/miss, per-hit damage and exhausted
receipt rejection in both modes, plus digital damage dice and legacy-reference
compatibility. Unknown Produce Flame/Flame Blade activations, Chromatic Orb bounces
and Steel Wind Strike's distinct-target choice remain unmodeled rather than receiving
an assumed fixed count.
