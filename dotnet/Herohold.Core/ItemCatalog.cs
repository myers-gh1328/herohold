namespace Herohold.Core;

/// <summary>
/// One item definition. Official items come from the rules data and never change; homebrew items are the ones a user makes.
/// A character's <see cref="EquipmentItem"/> points at one by <see cref="EquipmentItem.CatalogItemId"/> and keeps only its own state.
/// </summary>
public sealed record CatalogItem
{
    public const string Homebrew = "Homebrew";
    public string Id { get; init; } = string.Empty;
    /// <summary>The book an official item comes from (XPHB, XDMG), or <see cref="Homebrew"/>.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>gear, weapon, armor, shield, ammunition, potion, scroll, tool, focus or other.</summary>
    public string Kind { get; init; } = "gear";
    /// <summary>The user a homebrew item belongs to; null for official items.</summary>
    public string? OwnerId { get; init; }
    /// <summary>The definition: name, text, weight, weapon and bonuses. Its per-character state (quantity, equipped, charges left) is unused.</summary>
    public EquipmentItem Definition { get; init; } = new();
    public bool IsHomebrew => Source == Homebrew;

    public static string NewHomebrewId() => "homebrew:" + Guid.NewGuid().ToString("N");

    /// <summary>A character's copy: this definition with the character's own quantity, equipped, attuned, carried, charges left and scroll spell.</summary>
    public EquipmentItem Owned(EquipmentItem state) => SheetRules.CloneItem(Definition) with
    {
        Id = state.Id, CatalogItemId = Id, Quantity = state.Quantity, Equipped = state.Equipped, Attuned = Definition.RequiresAttunement && state.Attuned,
        Carried = state.Carried, ScrollSpell = state.ScrollSpell,
        Charges = Definition.Charges is { } charges ? charges with { Remaining = Math.Clamp(state.Charges?.Remaining ?? charges.Maximum, 0, charges.Maximum) } : null
    };

    /// <summary>The definition part of an owned item, without the character's state.</summary>
    public static EquipmentItem DefinitionOf(EquipmentItem item) => SheetRules.CloneItem(item) with
    {
        Id = string.Empty, CatalogItemId = string.Empty, Quantity = 1, Equipped = false, Attuned = false, Carried = true, ScrollSpell = string.Empty,
        Charges = item.Charges is { } charges ? charges with { Remaining = 0 } : null
    };
}

/// <summary>An official item a name stands for, the spell when it names a spell scroll, and how many single items one named unit holds ("Arrows (20)").</summary>
public sealed record ItemMatch(CatalogItem Item, string ScrollSpell, int UnitQuantity);

/// <summary>The official items of the rules data, and how a typed or imported item name finds one.</summary>
public interface IItemCatalog
{
    IReadOnlyList<CatalogItem> Official { get; }
    ItemMatch? Match(string name);
}

/// <summary>No official items: every item is homebrew. For stores used without the rules data.</summary>
public sealed class EmptyItemCatalog : IItemCatalog
{
    public static readonly EmptyItemCatalog Instance = new();
    public IReadOnlyList<CatalogItem> Official => [];
    public ItemMatch? Match(string name) => null;
}

public static class ItemLinking
{
    /// <summary>
    /// Whether an unlinked item can become the official item its name matches. An item with its own mechanics (bonuses,
    /// charges, effects, defenses, or a different weapon) stays homebrew so nothing it does is lost.
    /// </summary>
    public static bool FitsOfficial(EquipmentItem item, CatalogItem official)
        => item.AttackBonus == 0 && item.DamageBonus == 0 && item.ModifierBonuses.Count == 0 && item.Defenses.Count == 0
           && item.PassiveEffects.Count == 0 && item.UseEffects.Count == 0 && item.Charges is null
           && (item.BaseWeapon.Length == 0 || string.Equals(item.BaseWeapon, official.Definition.Name, StringComparison.OrdinalIgnoreCase));
}
