# Consumables plan — progress

Updated: 2026-11-01 (part 1 built)

## Goal
Shared item catalog (part 1), then ammunition, healing potions, thrown items, spell scrolls (parts 2–5), each its own PR.
D&D editions only; keep it simple: this is a character sheet.

## Owner decisions
- Shared `items` catalog with per-character ownership rows in `character_items` (approved 2026-10-31).
- Catalog rows keyed by source + item (XPHB, PHB, Homebrew); `characters.RulesSource` = XPHB; no edition picker yet.
- Basic ammo untracked by default; strict counting is a per-character switch.
- Homebrew items are shared across the owner's characters; homebrew means only items the owner creates.
- Official items are read-only (only per-character state changes).
- Matched items use the catalog's official text (Roll20 copies dropped; the backup keeps them).
- Scrolls are not homebrew: they become the official Spell Scroll with the spell kept (`ScrollSpell`); casting comes in part 5.
- Order of parts 2–5 is the agent's pick.
- Healing potions follow the app-wide Preferred dice mode toggle (Digital rolls, Physical asks for the total).
- Only Llayawin needs moving (Veyra Thorne is the built-in sample); back up first; check her sheet:
  Ranger 10, 71/81 HP, AC 18, Briarbloom Bow +12 1d8+6, Dragon Slayer Shortsword +11 1d6+7.

## Findings
- `data/raw/5etools/items.json` + `items-base.json` hold the full catalog (2,658 items, PHB/DMG and XPHB/XDMG):
  weight, type, ammoType, consumable tag (CNS), magic items. Effects (potion dice etc.) are prose only.
- Llayawin's item descriptions came from her Roll20 PDF import, not the catalog.

## Steps
1. Part 1 item catalog — built on `feat/item-catalog`, tests added; full check and owner question pending.
   Trial on a copy of the live DB: Llayawin Ranger 10, 71/81 HP, AC 18, bow +12 1d8+6, shortsword +11 1d6+7 (matches);
   18 items official, 2 scrolls -> Spell Scroll (Level 3) + spell, 5 homebrew.
   Found: Explorer's Pack now weighs 55 lb (catalog) while she also owns its contents (80 -> 135 lb carried; capacity 195).
   Item description limit raised 2,000 -> 10,000 (21 official items exceed 2,000).
2. Ammunition — not started.
3. Healing potions — not started.
4. Thrown items — not started.
5. Spell scrolls — not started.

## Last check
Full suite 3x green (901 tests each, 0 warnings); CI subset green without data; trial import unchanged from main.

## Next action
Full check (3 runs + trial import), commit, PR, merge. Ask owner about Llayawin's Explorer's Pack. Redeploy only on owner go.
