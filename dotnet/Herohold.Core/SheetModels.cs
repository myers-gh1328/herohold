namespace Herohold.Core;

public enum CoinChangeKind { Receive, Spend, Set }
public sealed record CoinPurse
{
    public int Cp {get;init;} public int Sp {get;init;} public int Ep {get;init;} public int Gp {get;init;} public int Pp {get;init;}
}
/// <summary>Null means use the character build/import source; an explicit empty value is a manual override.</summary>
public sealed record CharacterProfile
{
    public string? Background {get;init;} public string? Size {get;init;}
    public string? Appearance {get;init;} public string? Backstory {get;init;} public string? Personality {get;init;} public string? Alignment {get;init;}
    public string? PlayerName {get;init;} public string? Age {get;init;} public string? Height {get;init;} public string? Weight {get;init;}
    public string? Eyes {get;init;} public string? Skin {get;init;} public string? Hair {get;init;}
    public string? Ideals {get;init;} public string? Bonds {get;init;} public string? Flaws {get;init;}
    public string? Allies {get;init;} public string? Organizations {get;init;} public string? Treasure {get;init;}
    public List<string>? Languages {get;init;} public List<string>? ArmorTraining {get;init;}
    public List<string>? WeaponProficiencies {get;init;} public List<string>? ToolProficiencies {get;init;}
}
public sealed record DeathSaveState
{
    public int Successes {get;init;} public int Failures {get;init;}
    public bool Stable {get;init;} public bool Dead {get;init;}
}
public sealed record CharacterSheetState
{
    public AdventureFacts Adventure {get;init;}=new();
    public CharacterProfile Profile {get;init;}=new();
    public CoinPurse? Coins {get;init;}
    public int? ExperiencePoints {get;init;}
    public bool? HeroicInspiration {get;init;}
    public DeathSaveState DeathSaves {get;init;}=new();
    /// <summary>Whether attacks spend ammunition. Off by default: basic ammunition is not tracked.</summary>
    public bool CountAmmunition {get;init;}
    /// <summary>Ammunition fired since it was last recovered, by item name (Arrow), while counting is on.</summary>
    public Dictionary<string,int> AmmunitionFired {get;init;}=[];
}
public enum HeroicDieKind { D20, Damage, SpellEffect, BonusDie }
public sealed record HeroicInspirationRequest:PlayRequest
{
    public HeroicDieKind Kind {get;init;}
    public string RollId {get;init;}="";
    public string CastId {get;init;}="";
    public int DamageTermIndex {get;init;}
    public int? PhysicalDie {get;init;}
    public int DieIndex {get;init;}
    public int? PhysicalD20 {get;init;}
}
