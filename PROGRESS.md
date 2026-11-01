# Consumables, part 5 of 5: spell scrolls

Goal: consumables in five PRs (1 item catalog, merged #2; 2 ammunition, merged #3; 3 healing potions; 4 thrown items; 5 spell scrolls).

Owner decisions:
- Scrolls are the official Spell Scroll (Level N) with ScrollSpell; casting comes in this part.
- Dice follow the app-wide Preferred dice mode.
- Keep it simple: at its core this is a character sheet.

Steps:
1. Scroll spell lookup from the rules catalog (spell, level, class lists). Done.
2. Read scroll: on your list or unintelligible; check above your slot level; scroll spent; concentration started; combat casting time. Done.
3. Scroll dialog and Read scroll button in Inventory. Done.
4. Tests (Core, Integration, Components). Done.
5. Gate. Parts 2-4 merged (#3, #4, #5 390dd08); rebased onto main. Running.

Next action: gate, then push, PR, squash-merge.
Waiting on: gate.
Updated: 2026-11-01
