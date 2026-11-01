# Consumables, part 2 of 5: ammunition

Goal: consumables in five PRs (1 item catalog, merged #2; 2 ammunition; 3 healing potions; 4 thrown items; 5 spell scrolls).

Owner decisions:
- Basic ammunition is untracked by default; a per-character switch turns strict counting on.
- I choose the order of parts 2-5: ammunition, healing potions, thrown items, spell scrolls.
- Healing potions follow the app-wide Preferred dice mode (Digital: app rolls; Physical: enter the total).
- Scrolls are the official Spell Scroll (Level N) with ScrollSpell; casting comes in part 5.
- Keep it simple: at its core this is a character sheet.

Steps:
1. Ammunition type per weapon (rules data ammoType) on derived attacks. Done.
2. Count ammunition switch, spend on attack roll, block at zero, recover half. Done.
3. Storage column + fired counts, migration AmmunitionCounting. Done.
4. Inventory panel and attack-row counts. Done.
5. Tests (Core, Rules, Integration, Components). Done.
6. Gate: build 0 warnings, full suite 3x, CI subset, trial import. Running.

Next action: run the gate, then push, PR, squash-merge; then start part 3 on a new branch.
Waiting on: nothing.
Updated: 2026-11-01
