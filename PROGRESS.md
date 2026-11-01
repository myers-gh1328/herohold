# Consumables, part 3 of 5: healing potions

Goal: consumables in five PRs (1 item catalog, merged #2; 2 ammunition; 3 healing potions; 4 thrown items; 5 spell scrolls).

Owner decisions:
- Healing potions follow the app-wide Preferred dice mode (Digital: app rolls; Physical: enter the total).
- No homebrew are custom items: official items are read-only; homebrew items are the user's own.
- I choose the order of parts 2-5: ammunition, healing potions, thrown items, spell scrolls.
- Keep it simple: at its core this is a character sheet.

Steps:
1. Healing dice on items, parsed from the potions' rules text (4 official potions). Done.
2. Using a healing item spends it and heals the rolled or entered total (checked against the dice range). Done.
3. Storage columns + migration PotionHealing; homebrew healing field. Done.
4. Item use dialog: digital roll with dice shown, physical total; inventory "Heals" line. Done.
5. Tests (Core, Integration, Components). Done.
6. Gate. Part 2 merged (#3, f494658); rebased onto main. Running.

Next action: gate, then push, PR, squash-merge.
Waiting on: gate.
Updated: 2026-11-01
