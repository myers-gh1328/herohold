# Owned character-sheet state

`CharacterState.Sheet` adds editable profile references, XP, a five-denomination
purse, held Heroic Inspiration and death-save/stable/dead state. Old workspaces
omit this property and keep their normal initializer. Loading does not save or
migrate the file. Canonical JSON and PDF attachments round-trip the owned sidecar;
manual profile references never overwrite the character builder's rule choices.

Null profile values, coins, XP and inspiration use explicit canonical/import
fallbacks. Empty profile strings/lists are deliberate manual overrides. Updating
an unrelated profile field with null XP preserves the existing XP source.
Unrepresentable imported numbers require review; they are preserved on export
until the player explicitly records a replacement. Coins use bounded integers,
remain separate denominations, and compound receive/spend/set is atomic.

All session editors capture character identity. A queued editor cannot mutate the
character selected later; repository revisions still protect concurrent saves.
Owned records and collection members are detached before mutation/storage.

## Survival and exhaustion

[2024 damage/healing rules](https://www.dndbeyond.com/sources/dnd/br-2024/playing-the-game#Droppingto0HitPoints)
control death saves. Ordinary success/failure uses its adjusted total; natural 1
causes two failures and natural 20 restores 1 HP. Three successes stabilize and clear
both counters; three failures kill. Healing a living character clears the counts.
Ordinary healing does not revive a dead character; a deliberate manual correction
can record revival/adjudication. Temporary HP never count as healing, and any
damage at zero HP still causes failures even if temporary HP absorb it. Critical
damage at zero adds two failures; massive damage records death.

[2024 Exhaustion](https://www.dndbeyond.com/sources/dnd/br-2024/rules-glossary#Exhaustion)
penalties are source terms on actual D20 calculations: ability checks,
skills, saves, weapon/spell attacks and initiative. Base modifiers, AC, damage and
spell DCs stay unchanged. Effective speed is base speed minus 5 feet per level,
floored at zero. Six levels record death; merely reducing the level does not revive.
Builder/canonical standing numbers exclude transient Exhaustion so projection,
recalculation and export do not turn the penalty into a permanent adjustment.
Live sheet and PDF references apply it once.

## Heroic Inspiration

[2024 Heroic Inspiration](https://www.dndbeyond.com/sources/dnd/br-2024/rules-glossary#HeroicInspiration)
is separate from Lucky. `UseHeroicInspiration` replaces exactly one selected die,
uses the replacement even when lower, recalculates the result and consumes the
held flag atomically. Supported recorded contexts are unresolved D20s and bonus
dice, just-resolved weapon damage, and resolved spell-effect dice. Stored die
sizes bound physical entries. Outcome window IDs reject stale rerolls after a
later play action or confirmed profile/coin/item/note change; old outcomes without an eligible window cannot be rerolled.
Staged rest/level-up dice and tabletop dice without an authoritative outcome are
not silently claimed as automated reroll workflows.

## Equipment facts

Items can hold `Defenses`, `PassiveEffects` and `UseEffects`. Passive facts derive
from quantity/equipment/attunement instead of being copied into manual lists.
`EffectiveDefenses`/`EffectiveEffects` retain each source; removing one item does
not erase another source of the same type/condition. The zero-HP Unconscious
reference is likewise derived and disappears on healing while unrelated manually
recorded conditions remain. `UseEquipment` decrements consumables and adds distinct
sourced ongoing effects. Item-specific triggers/durations remain explicit data or
table-managed reference rather than an invented universal magic-item engine.

## Verification (phase one)

Behavior-first regression runs observed missing sheet/survival behavior, XP export
returning0 instead of7777, exhausted standings being baked into exported numbers,
legacy imported amounts becoming0, and stale Inspiration surviving a coin change.
The implementations then passed130 Core cases (the user-deferred proficiency
matrix excluded),7 sheet-session integration cases and26 Documents cases.
These checks cover persistence, guarded public session methods, canonical/PDF
round trips, backward-compatible file reads, source isolation, survival boundaries,
Exhaustion and four recorded Inspiration contexts. No deployed application or
player save was changed. Native component and browser verification is owned by the
UI/integration reviewers, not claimed by this Core contract.


### Equipment incapacitation follow-up

Inventory mutations reconcile effective incapacitating conditions from active
item sources. They end stored Concentration and clear the Hunter's Mark target;
removing a source never restores ended Concentration. Starting a new concentration
effect checks effective item conditions as well as manual conditions. Passive
editor entries cannot start Concentration; on-use entries can explicitly require
this character to maintain it. Existing imported metadata is retained.

`PassiveIncapacitationTests` exercises equip/end and blocked restart;
`NativeItemFactsTests` exercises passive/on-use editor switching.

## Source-owned magic and mixed Hit Dice

Explicit `Play.SpellSources` owns spell memberships, casting abilities and independent
preparation/cantrip policies. `Preparation.MaximumSpellLevel` limits source access;
shared higher-level slots and Pact slots permit upcasting but never expand that
source's eligible preparations. An owned spell ID has exactly one source; catalog
content can still back multiple owned IDs. Existing grant provenance IDs may contain
punctuation and remain unchanged, while owned spell/resource/track/pool entity IDs
retain the normal bounded safe-ID contract.

A rest defaults to no spell changes. `RestRequest.SourceChanges` identifies each
opted-in source; changes validate before any RNG. `HitDieSpends` identifies each
mixed-size pool and its unmodified physical results. The native rest dialog stages
one selected pool die at a time, shows the cumulative preview and persists only on
confirmation. Long Rests restore all Hit Dice under the 2024 rules. Old scalar callers
remain valid without explicit sources/pools; ambiguous source/pool requests reject.
Conflicting explicit and legacy requests reject rather than choosing silently.

Rest snapshots deep-copy source policies and pool memberships. Undo restores only
sources/pools changed by that action and preserves unrelated later source edits;
overlapping edits reject. Canonical export writes class-owned names into each matching
`folio_class_tracks[].choices.spells` while retaining stable owned IDs in `folio_play`.
Metadata projection accepts `spellSources`, `ownedSpells`, `hitDiePools`, preserves spent
Hit Dice across capacity changes and keeps existing grant/resource receipts intact.
No workspace is rewritten by opening it.

These state guarantees do not claim every multiclass composition/class-feature adapter
is complete; see the separate multiclass implementation contract and its verified
builder/level-up matrix. Tests live in `MulticlassPlayTests`,
`MulticlassRestSessionTests`, and `MulticlassRestUiTests`.
