# Consumables plan — progress

Updated: 2026-11-01

## Goal
Shared item catalog (part 1), then ammunition, healing potions, thrown items, spell scrolls (parts 2–5), each its own PR.
D&D editions only; keep it simple: this is a character sheet.

## Owner decisions
- Shared `items` catalog with per-character ownership rows in `character_items` (approved 2026-10-31).
- Catalog rows keyed by source + item (XPHB, PHB, Homebrew); `characters.RulesSource` = XPHB; no edition picker yet.
- Basic ammo untracked by default; strict counting is a per-character switch.
- Homebrew items are shared across the owner's characters.
- Order of parts 2–5 is the agent's pick.
- Healing potions follow the app-wide Preferred dice mode toggle (Digital rolls, Physical asks for the total).
- Only Llayawin needs moving (Veyra Thorne is the built-in sample); back up first; check her sheet:
  Ranger 10, 71/81 HP, AC 18, Briarbloom Bow +12 1d8+6, Dragon Slayer Shortsword +11 1d6+7.

## Findings
- `data/raw/5etools/items.json` + `items-base.json` hold the full catalog (2,658 items, PHB/DMG and XPHB/XDMG):
  weight, type, ammoType, consumable tag (CNS), magic items. Effects (potion dice etc.) are prose only.
- Llayawin's item descriptions came from her Roll20 PDF import, not the catalog.

## Steps
1. Part 1 item catalog — in progress (branch `feat/item-catalog`): design questions open.
2. Ammunition — not started.
3. Healing potions — not started.
4. Thrown items — not started.
5. Spell scrolls — not started.

## Last check
None yet on this branch.

## Next action
Owner question: what editing a catalog item does (becomes shared homebrew vs per-character note).
