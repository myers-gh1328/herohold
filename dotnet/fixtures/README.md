# Bounded Python / C# calculation comparison

Run from the repository root:

```sh
.venv/bin/python tools/compare_dotnet.py
.venv/bin/python -m pytest tests/fixtures/dotnet/test_comparison_harness.py -q
```

The tool builds `dotnet/Herohold.Parity`, passes only `calculations.json`, and checks
Python **and** C# independently against the explicit expected results. A case fails
even when both engines return the same wrong result. `--python-only` validates the
Python expectations without claiming C# parity; `--no-build` uses an existing
Debug build. A missing fixture, missing/duplicate result, disagreement, build
failure, or timeout exits nonzero.

The 18 fictional cases cover low odd scores and floor division; proficiency
thresholds; ordinary proficiency and Expertise; saving throws and weapon accuracy;
Gloom Stalker initiative eligibility; and active/inactive item modifier sources.
The C# runner checks source terms sum to totals, repeated reads do not compound
bonuses, and the input character remains unchanged, including HP/resources.

Expected results are written explicitly: for example, score9 gives−1;
level5 PB3; Wisdom16+charm1 gives modifier4; Perception Expertise gives4+6=10;
Dexterity18+PB3+Archery2 gives longbow accuracy9; and Gloom initiative is4+4=8.
C# and Python implementations do not generate the expected values.

## Adapter boundaries

Existing Python does not distinguish a carried item from an equipped/attuned one.
The comparison adapter deliberately passes **only active modeled fixture items**
into its existing equipment helper; stable fixture IDs become synthetic Python
item names. This proves the bounded modifier arithmetic after activation, while
C# tests establish new activation behavior. It does not prove that legacy saves
already preserve equipment state or that legacy inventory was migrated.

The weapon adapter exercises public Python Longsword/Longbow profiles, with no
flat modifier or Archery+2 only. Other attacks need explicit fixtures/adapters.
Initiative uses a detached synthetic character and the real Python recalculation
branch with empty source catalogs, rather than duplicating its formula here.

This is not all-class progression, spell execution, PDF, legacy-save, currency,
attunement-limit, or whole-character parity. No private saves/uploads are read.

## Native HP and class progression comparison

`native_rules.json` adds 159 independently reviewed synthetic cases: 65 HP cases
cover all twelve hit dice/class combinations at levels1/3/5/11/20, explicit
rolled history, retroactive Constitution, Tough, Dwarven Toughness, imported
adjustment, current-HP downward clamping, additional maximum bonuses, and rejection
of explicit first-level Fixed/Rolled methods. The94
class cases cover all twelve classes at the same levels and Eldritch Knight/
Arcane Trickster at1/2/3/5/11/20. Explicit slot tables independently check full,
half, pact and third casters; public Python helpers additionally compare complete
features, resources, choices, typed casting limits and subclass grants.
Life Domain, Oath of Devotion, Gloom Stalker, Celestial Patron and Abjurer add
twenty cases with explicitly reviewed cumulative prepared-spell/cantrip lists.

Reviewed corrections deliberately diverge from legacy output: minimum1 HP applies
at each level, first-level HP must use the maximum die, third-caster slots cannot
appear before level3, and early Celestial subclass grants remain inactive before
the class's selection threshold. Fixtures reproduce the legacy defect separately
instead of blessing it as parity.

After building Herohold.Parity, run:

```sh
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --rules tests/fixtures/dotnet/native_rules.json data/imported/5etools/classes.json > .tmp/native-rules-results.json
.venv/bin/python tools/compare_dotnet_rules.py tests/fixtures/dotnet/native_rules.json .tmp/native-rules-results.json
```

The existing numerical comparison invocation is unchanged. This new comparison
does not establish native character creation, feat eligibility, level-up choice
execution, PDF handling, or total removal of the transition Python bridge.

## Ability and standing derivations

`derivations.json` contains212 isolated cases:86 score derivations,104 standing
derivations and22 dependent resource/DC cases. Frozen expected totals are independent of either engine. All54
catalog fixed/choose feat score sources exercise the public Python feat selection
constructor; ASI+2/split+1 exercise its separate constructor. Source ordering,
background caps, class level20 increases, partial caps, manual scores above normal
caps, and epic caps are explicit. Standing cases cover scores1–30, PB thresholds,
item activation/attunement/quantity, all five modeled numeric proficiency feats,
feat order, base proficiency versus expertise, and no duplicated half-feat raises.

`legacyOverrides` records reviewed corrections: positive capped increases never
lower scores, background bonuses stop at20 without lowering higher manual scores,
Bard Jack of All Trades affects only nonproficient skill checks, and the typed
Thaumaturge/Magician choices add final Wisdom modifier (minimum1) to their sourced
skills. Explicit rejection cases cover invalid sources, choices and bounds.
The dependent cases check inspiration/veil/dread/ward/priest/flare/corona/glory
capacities, minimum uses, exact recovery changes, Psi Warrior target counts,
spell/maneuver/Shield Master DCs and Great Weapon Master PB values against public
Python helpers and independent expected numbers. Run the CLI from the repository
root; this section reads only the fixed normalized class catalog.

```sh
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --derivations tests/fixtures/dotnet/derivations.json > .tmp/native-derivation-results.json
.venv/bin/python tools/compare_dotnet_derivations.py tests/fixtures/dotnet/derivations.json .tmp/native-derivation-results.json
```

The runner also verifies contribution sums, input purity and repeated evaluation.
Standing inputs are final scores and pre-feat proficiency sets; deriving those
sets from a canonical character belongs to the separately reviewed build adapter.
This fixture gate does not establish feat eligibility, spell grants, native
creation or live application cutover. No private files are read.

## Native catalog snapshot

`catalog.json` supplies independent facts for all seven public catalog arrays:
12 classes,10 species,16 backgrounds,55 feats,391 spells,217 equipment strings,
and41 weapons. The fixed comparison checks complete JSON recursively, including
unknown fields, scalar types and array order. Independent sentinels check ritual
flags, cleaned weapon-proficiency text and equipment ordering; agreement between
two equally wrong results cannot satisfy those facts.

Three synthetic cases additionally cover nested unknown fields, tagged versus
untagged whitespace, single-pass nested-tag cleanup, third nonempty pipe fields,
DC/filter tags, last matching raw spell, source/name case, ritual truthiness and
equipment shape rejection. The native runner mutates returned records, nested
objects/arrays and envelopes to verify private snapshot isolation. Python source
inputs and requests remain unchanged.

```sh
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --catalog . tests/fixtures/dotnet/catalog.json > .tmp/native-catalog-results.json
.venv/bin/python tools/compare_dotnet_catalog.py tests/fixtures/dotnet/catalog.json .tmp/native-catalog-results.json
```

Only fixed repository catalog assets and synthetic JSON are read. This comparison
does not establish a native replacement for creation, validation or rule execution.

## Armor and movement derivations

`defense_rules.json` contains111 expected cases:58 armor and53 movement. Standard
armor bases, positive/negative Dexterity, shields, exact aliases, activation,
Medium Armor Master, and class movement gates have explicit expected results.
Four shield/subclass cases assert reviewed corrections to the Python reference:
Monk/Dance defense requires no Shield, Draconic defense permits it, and subclass
formulas are inactive before selection level3. Comparison checks expected values
in each engine, plus native term sums and input purity.

```sh
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --defenses tests/fixtures/dotnet/defense_rules.json > .tmp/native-defense-results.json
.venv/bin/python tools/compare_dotnet_defenses.py tests/fixtures/dotnet/defense_rules.json .tmp/native-defense-results.json
```

Movement uses a resolved native Monk class bonus and trusted species base speed;
it does not infer temporary spells, custom magic-item effects, or heavy-armor
Strength penalties. These remain separate modeling boundaries.

## Class, feat and creation choices

The builder fixtures add847 cases:482 class choices,275 feat choices and90
creation descriptors. `class_choices.json` covers all twelve classes at every
level1–20, all48 subclasses below/at/after selection, cumulative quotas, owned
versus other-source proficiency grants, invocation prerequisites, dependent
cantrips, Savant spells and late Wizard choices. Explicit action traces reopen
selections, change a parent, retain invalid raw values and repair the selection.

`feat_choices.json` covers all55 catalog feats at four eligibility boundaries,
complete child option arrays, synthetic prerequisite OR/AND groups, edited versus
other feat instances, unavailable required choices, exact allocations and child
selection validation. An allocation at its score cap remains legal: the separate
numeric derivation gate proves that the actual score increase is capped.

`creation_choices.json` checks every class's starting choice/spell quotas, all16
backgrounds, all10 species, lineage casting abilities, independent Magic Initiate
and tool-feat sources, invalid partial drafts, optional Wizard preparation, Bard
expanded spell lists and Savant's locked book union. Savant preserves the raw
ordinary book selection; `details.effectiveSelection` includes its free spells.
Duplicate canonical path owners fail the comparison.

Both implementations are checked against explicit reviewed expectations.
`legacyExpected` records historical defects and narrower legacy ownership;
native expected values remain authoritative. Full real catalog option arrays
are additionally compared for cases without an intentional correction. Native
inspection must be repeatable and leave caller-owned selections unchanged.
Harness tests reject wrong membership, counts, types, statuses, duplicate IDs,
duplicate paths and missing results. Cases use fictional characters only.

Reviewed2024 corrections include unrestricted Fighting Style feat selection for
Paladin/Ranger, Ranger Expertise requiring a skill lacking other-source Expertise,
Scholar requiring proficiency, class-associated damaging cantrips for invocations,
distinct eligible selections, inactive dependent choices and actual spell-list
membership. The primary references are the official [2024 class rules](https://www.dndbeyond.com/sources/dnd/br-2024/character-classes)
and [feat rules](https://www.dndbeyond.com/sources/dnd/br-2024/feats).

```sh
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --class-choices . tests/fixtures/dotnet/class_choices.json > .tmp/native-class-choices.json
.venv/bin/python tools/compare_dotnet_class_choices.py tests/fixtures/dotnet/class_choices.json .tmp/native-class-choices.json
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --feat-choices . tests/fixtures/dotnet/feat_choices.json > .tmp/native-feat-choices.json
.venv/bin/python tools/compare_dotnet_feat_choices.py tests/fixtures/dotnet/feat_choices.json .tmp/native-feat-choices.json
dotnet dotnet/Herohold.Parity/bin/Debug/net10.0/Herohold.Parity.dll --creation-choices . tests/fixtures/dotnet/creation_choices.json > .tmp/native-creation-choices.json
.venv/bin/python tools/compare_dotnet_creation_choices.py tests/fixtures/dotnet/creation_choices.json .tmp/native-creation-choices.json
.venv/bin/python -m pytest tests/fixtures/dotnet/test_class_choice_comparison.py tests/fixtures/dotnet/test_feat_choice_comparison.py tests/fixtures/dotnet/test_creation_choice_comparison.py -q
```

These are isolated rules/descriptor gates. They do not establish live native
creation, level-up transaction execution, PDF parity or removal of Python from
the application runtime. Python in these tools is a development reference only.


## Proficiency reference matrix

`proficiency_matrix.json` supplies independent proficiency and Expertise tables
for all 20 levels. One parameterized test method checks all three training states
at each level (60 cases), plus six active-item cases covering the level-5 and
level-9 examples. It checks all 18 skills and all six saving throws/attack abilities.

These 66 focused cases verify correct training bonuses, non-stacking Expertise,
item bonuses applied once, and matching displayed breakdowns. Ability-score
rounding/range tests remain separate; this matrix does not repeat their entire
cross-product. Only the governing ability varies within each check, so a wrong
ability lookup cannot hide behind uniform scores.

The dataset/test changes were compiled without executing the tests, per the
user's request. Existing threshold fixtures remain separate prior evidence.
