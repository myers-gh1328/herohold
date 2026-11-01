# Consumables, part 4 of 5: thrown items

Goal: consumables in five PRs (1 item catalog, merged #2; 2 ammunition; 3 healing potions; 4 thrown items; 5 spell scrolls).

Owner decisions:
- Thrown items: Alchemist's Fire, Acid, Holy Water, Oil (Net matches the same rule and is included).
- Dice follow the app-wide Preferred dice mode.
- I choose the order of parts 2-5. Keep it simple: at its core this is a character sheet.

Steps:
1. Thrown range and damage on items, parsed from the rules text (5 official items). Done.
2. Throw: spends one; in combat uses one attack of the Attack action on your own turn. Done.
3. Storage columns + migration ThrownItems; homebrew fields. Done.
4. Throw rows with the attacks, throw dialog with DC and damage roll. Done.
5. Tests (Core, Integration, Components). Done.
6. Gate. Parts 2 (#3) and 3 (#4, d032407) merged; rebased onto main. Running.

Next action: gate, then push, PR, squash-merge.
Waiting on: gate.
Updated: 2026-11-01
