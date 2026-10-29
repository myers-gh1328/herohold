namespace Herohold.Core;

public enum ItemUseAction { TableManaged, Action, BonusAction, Reaction, None }

public sealed record ItemCharges
{
    public int UseCost {get;init;}=1;
    public int Maximum {get;init;}
    public int Remaining {get;init;}
    public string RecoveryNote {get;init;}="";
}
public sealed record MovementMode
{
    public string Kind {get;init;}="Swim";
    public int Speed {get;init;}
    public string Source {get;init;}="";
    public string Note {get;init;}="";
}
public sealed record CompanionAction
{
    public string Name {get;init;}="";
    public string Rules {get;init;}="";
}
public sealed record CompanionSheet
{
    public string Id {get;init;}="";
    public string Name {get;init;}="";
    public string Kind {get;init;}="Companion";
    public int ArmorClass {get;init;}=10;
    public int MaximumHp {get;init;}=1;
    public int CurrentHp {get;init;}=1;
    public int TemporaryHp {get;init;}
    public int Speed {get;init;}=30;
    public string Source {get;init;}="";
    public string Notes {get;init;}="";
    public List<CompanionAction> Actions {get;init;}=[];
    public List<ResourcePool> Resources {get;init;}=[];
    public List<string> Conditions {get;init;}=[];
}
public sealed record AdventureFacts
{
    public List<MovementMode> Movement {get;init;}=[];
    public List<CompanionSheet> Companions {get;init;}=[];
    public decimal? CapacityOverrideLb {get;init;}
    public string CapacitySource {get;init;}="";
    public bool CarryCoins {get;init;}=true;
}
public sealed record CarryingSummary(decimal KnownWeightLb,decimal CapacityLb,int UnweighedItems,bool Complete)
{
    public bool OverCapacity=>KnownWeightLb>CapacityLb;
}
