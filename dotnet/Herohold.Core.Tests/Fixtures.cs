namespace Herohold.Core.Tests;

internal static class Fixtures
{
    public static CharacterState Ranger() => new()
    {
        Id = "ranger-demo", Name = "Veyra Thorne", Species = "Elf", ClassName = "Ranger", Subclass = "Gloom Stalker",
        Level = 5, CurrentHp = 32, MaximumHp = 44, ArmorClass = 16, Speed = 30,
        AbilityScores = new() { [Ability.Strength] = 12, [Ability.Dexterity] = 18, [Ability.Constitution] = 14, [Ability.Intelligence] = 10, [Ability.Wisdom] = 16, [Ability.Charisma] = 10 },
        SaveProficiencies = [Ability.Strength, Ability.Dexterity],
        SkillProficiencies = ["Perception", "Stealth"], Expertise = ["Perception", "Stealth"],
        Equipment = [new() { Id = "charm", Name = "Wayfinder charm", Quantity = 1, Equipped = true, Attuned = true, RequiresAttunement = true, ModifierBonuses = new() { [Ability.Wisdom] = 1 } }, new() { Id = "potion", Name = "Healing potion", Quantity = 3, Consumable = true }],
        Resources = [new() { Id = "luck", Name = "Luck", Maximum = 3, Remaining = 3 }]
    };
}
