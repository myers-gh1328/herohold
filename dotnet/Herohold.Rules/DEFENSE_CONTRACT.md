# Native armor and movement

`ArmorRules.Calculate(ArmorInput)` returns candidate formula calculations and the
highest applicable result. `MovementRules.Calculate(MovementInput)` returns final
speed, Roving climb/swim modes, active calculation terms and inactive conditional
feature terms. Public DTOs are in DefenseModels.cs. Inputs are never mutated and
outputs contain detached read-only term lists.

Equipment follows Core activation: positive quantity, equipped, and attuned when
required. Duplicate IDs and invalid/bounded collections reject. All12 standard
armor types are explicit. Names match case-insensitively after trimming; an exact
optional final ` Armor` alias is allowed only for those known names. Custom magic
armor properties are never inferred from a substring. Multiple marked armor items
provide alternative formulas, never stacked armor bases.

Light armor adds full resolved Dexterity; medium caps it at2 or3 for Medium Armor
Master with actual Dexterity score16+. Heavy armor adds no Dexterity. Negative
Dexterity applies to light/medium. A Shield adds2 to eligible formulas; Defense
adds1 once only with active recognized armor. No unarmored formula is eligible
while active armor exists.

Barbarian Unarmored Defense allows a Shield. Monk and College of Dance defenses
require no armor AND no Shield. Draconic Sorcery3+ requires no armor and its text
does not prohibit a Shield. Subclass formulas remain inactive before3. The latter
restrictions correct the legacy engine's missing shield eligibility checks:
simply withholding a Shield's2 while retaining an ineligible formula is wrong.
Reviewed source: normalized XPHB feature descriptions for Monk Unarmored Defense,
Dance's parent Dazzling Footwork and Draconic Resilience. No text parser executes
these descriptions at runtime.

Movement uses the caller's trusted species base speed and native class catalog's
resolved Monk movement bonus. Speedy adds10, Boon of Speed30, each once by feat
identity. Barbarian5+ Fast Movement and Ranger6+ Roving add10 except in heavy
armor. Roving climb/swim equal final speed even while heavy armor suppresses its
increase. Monk movement requires neither armor nor Shield. Other species modes,
temporary spells, heavy-armor Strength penalties and arbitrary magic-item effects
are outside this module and must be retained or modeled by separate sources.

Five meaningful tests failed on absent rules before implementation. They now
exercise48 standard armor/Dexterity/Shield combinations plus aliases, inactive
items, MAM, class/subclass shield eligibility, deduped feats and movement gates.
No live Web cutover until independent source review and comparison are accepted.
