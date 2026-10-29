# Native single-class level-up

`NativeLevelUpRules` has no process, file, network or persistence access. Its
constructor receives the fixed catalogs, `FeatSelectionRules`,
`CharacterEditRules` and `ICharacterDraftRules`. The native builder implements
the latter's pure `Recalculate`, `Describe` and `FeatContext` operations.

`Start(source,target)`, `Choose(source,draft,id,value)` and
`Preview(source,draft,hp?)` return `LevelUpReview`. `Commit` returns the same
review with a completed draft and the detached candidate's transaction/history
records. Root application code owns atomic persistence, revision comparison and
translation into the existing workflow JSON. Reading or previewing does not save.

`LevelUpDraft` contains only transaction ID, full source hash, current/target
level, class, closed selection values and completion state. Candidate characters,
choice options, statuses and caller-supplied validation are never authoritative.
Every operation verifies the current source remains complete and matches the
draft; the target must be exactly the next level, within1–20, for one class.
The canonical hash sorts object keys and includes HP, inventory, resources,
unknown safe sidecars and all other fields. Array order remains significant.
Bounds are8MiB/depth64/250000nodes for source hashing and100 bounded selections.
Error-severity issues block confirmation; historical warnings remain visible
without preventing a valid transaction.

Pending choice IDs retain catalog ASI/subclass IDs and established class/spell
IDs where possible. Multi-choice quotas represent **new additions**, with old
values locked in `details.existingValues`; applying a selection appends those
values instead of replacing the saved list. Wizard free Savant spells participate
in the effective book quota while the ordinary book remains separate. Rebuilding
after a parent change reads fresh trusted descriptors. Inactive selections remain
in the detached draft and do not write inactive character paths.
Pending `details.effectiveSelection` contains only the new selection consumed by
the generic editor; `effectiveTotalSelection` retains the full preview when it
was supplied by creation rules. This prevents old values filling a new quota.

ASI and feat selections are validated against the character before their own
proposed grants. Feat instances and linked score adjustments have deterministic
transaction-derived identities, so repeated previews do not stack either source.
`details.feat_options` includes eligibility, disabled reasons and recursive generic
`children`. Skill Expert's proposed new proficiency becomes available in its own
Expertise child before the feat is complete; an incomplete feat grants nothing.

Existing Ritual Caster instances receive only the additional level1 rituals
required by the new proficiency bonus. Their old spells, instance identity and
ability adjustment remain unchanged. Legacy named instances without a feat ID
resolve the known catalog ID without inventing a new feat source.

Spell changes always require an explicit optional selector. Normal class spell
growth appends existing choices. Bard, Sorcerer and Warlock can replace one
ordinary level1+ spell; Bard, Cleric, Druid, Sorcerer and Warlock can replace one
ordinary cantrip. Blessed/Druidic Warrior can replace one of their own cantrips
on the receiving class's level. Wizard cantrip replacement remains a Long Rest
operation. Warlocks can replace one existing Arcanum with a same-level Warlock
spell. Each active Magic Initiate owner can replace one of its own spells with a
same-level spell from that owner's list; its casting ability and other sources
stay unchanged. Generic options cannot change a whole prepared list as a rest.

HP uses an explicit fixed gain or unmodified physical Hit Die and records
`hp_history`. Recalculation includes retroactive Constitution and other resolved
maximum-HP features before review. Current and temporary HP are preserved and
clamped; only `AddGainedHp=true` adds the actual maximum increase to current HP.
Level-up never performs a rest. Canonical play state remains unchanged; the
application projection separately preserves spent pools when spell replacement
renames a grant resource.

This module delegates class feature/choice eligibility and numeric effects to
their native rules modules. It does not itself automate every descriptive class
feature or every optional class-feature replacement. Those capabilities require
their own explicit feature contracts and tests. Multiclass progression is outside
this contract.

Primary policies: [2024 character classes](https://www.dndbeyond.com/sources/dnd/br-2024/character-classes)
and [Magic Initiate's Spell Change](https://www.dndbeyond.com/sources/dnd/br-2024/feats#MagicInitiate).
