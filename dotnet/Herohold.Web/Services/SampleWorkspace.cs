using Herohold.Core;

namespace Herohold.Web.Services;

public static class SampleWorkspace
{
    public const string CharacterId = "veyra-thorne";

    public static WorkspaceSnapshot Create() => new()
    {
        Character = new CharacterState
        {
            Id = CharacterId, Name = "Veyra Thorne", Build=BuildFacts.FromCanonical(SampleCanonical.Create()), Species = "Elf", ClassName = "Ranger",
            Subclass = "Gloom Stalker", Level = 5, CurrentHp = 32, MaximumHp = 44,
            ArmorClass = 16, Speed = 30, Play = CreatePlay(),
            Features = ["Favored Enemy", "Deft Explorer", "Archery", "Dread Ambusher"],
            AbilityScores = new()
            {
                [Ability.Strength] = 12, [Ability.Dexterity] = 18, [Ability.Constitution] = 14,
                [Ability.Intelligence] = 10, [Ability.Wisdom] = 16, [Ability.Charisma] = 10
            },
            SaveProficiencies = [Ability.Strength, Ability.Dexterity],
            SkillProficiencies = ["Athletics", "Perception", "Stealth", "Survival"],
            Expertise = ["Perception", "Stealth"],
            Equipment =
            [
                new(){Id="studded-leather",Name="Studded Leather Armor",Equipped=true,Description="Light armor. Base AC12 plus Dexterity."},
                new() { Id = "wayfinder-charm", Name = "Wayfinder charm", Quantity = 1,
                    Equipped = true, RequiresAttunement = true, Attuned = true,
                    Description = "Campaign item. While equipped and attuned: +1 to your Wisdom modifier; your score stays 16.",
                    ModifierBonuses = new() { [Ability.Wisdom] = 1 } },
                new() { Id = "longbow", Name = "Longbow", Equipped = true,
                    Description = "Ranged weapon · 1d8 Piercing. Archery adds +2 to attack accuracy." },
                new() { Id = "shortsword", Name = "Shortsword", Equipped = true,
                    Description = "Finesse weapon · 1d6 Piercing." },
                new() { Id = "arrows", Name = "Arrows", Quantity = 20, Consumable = true,
                    Description = "Ammunition. Use the counter when you fire an arrow." },
                new() { Id = "healing-potion", Name = "Potion of healing", Quantity = 3, Consumable = true,
                    Description = "Consume one to reduce the count. Roll healing at the table; HP changes are entered separately." },
                new() { Id = "travel-cloak", Name = "Travel cloak", Quantity = 1,
                    Description = "Carried in your backpack. This ordinary cloak grants no calculated bonus." }
            ],
            Resources =
            [
                new() { SourceKind="build", Id = "luck", Name = "Luck points", Remaining = 3, Maximum = 3 },
                new() { SourceKind="build", Id = "dread", Name = "Dreadful Strike", Remaining = 4, Maximum = 4 },
                new() { SourceKind="build", Id = "hunters", Name = "Free Hunter’s Mark", Remaining = 3, Maximum = 3 },
                new() { SourceKind="build", Id = "slot1", Name = "1st-level slots", Remaining = 4, Maximum = 4 },
                new() { SourceKind="build", Id = "slot2", Name = "2nd-level slots", Remaining = 2, Maximum = 2 },
                new() { SourceKind="build", Id = "hitdice", Name = "Hit Dice", Remaining = 5, Maximum = 5 }
            ],
            Notes = [new() { Id = "sample-note-1", Title = "The crossing at Fenmere",
                Text = "Meet the ferryman at first light. The northern road is flooded; ask whether the old causeway is passable.",
                CreatedAt = new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero) }]
        }
    };
    public static PlayState CreatePlay()=>new()
    {
        SpellcastingAbility=Ability.Wisdom,HitDieSize=10,
        PreparedSpellIds=["cure-wounds","fog-cloud","jump","hail-of-thorns","pass-without-trace","lesser-restoration"],
        GrantedSpellIds=["hunters-mark","disguise-self","rope-trick"],Spells=SpellCatalog.All.ToList(),
        Preparation=new(){CanChangeOnLongRest=true,PreparedLimit=6,ReplacementLimit=1,
            EligibleSpellIds=["cure-wounds","fog-cloud","jump","hail-of-thorns","pass-without-trace","lesser-restoration","goodberry","ensnaring-strike"]}
    };

}
