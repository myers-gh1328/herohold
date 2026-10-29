namespace Herohold.Core;

public sealed record SpellSourceState
{
    public string Id { get; init; } = "";
    public string? ClassTrackId { get; init; }
    public string SourceKind { get; init; } = "class";
    public string Name { get; init; } = "";
    public Ability Ability { get; init; }
    public PreparationPolicy Preparation { get; init; } = new();
    public List<string> PreparedSpellIds { get; init; } = [];
    public List<string> KnownSpellIds { get; init; } = [];
    public List<string> SpellbookIds { get; init; } = [];
    public List<string> GrantedSpellIds { get; init; } = [];
}
public sealed record HitDiePool
{
    public string Id { get; init; } = "";
    public int Sides { get; init; }
    public int Maximum { get; init; }
    public int Remaining { get; init; }
    public List<string> ClassTrackIds { get; init; } = [];
}
public sealed record SourcePreparationChange
{
    public string SpellSourceId { get; init; } = "";
    public bool ChangeSpells { get; init; }
    public List<string> NewPreparedSpellIds { get; init; } = [];
    public bool ChangeCantrips { get; init; }
    public List<string> NewClassCantripSpellIds { get; init; } = [];
}
public sealed record HitDieSpend
{
    public string PoolId { get; init; } = "";
    public int Count { get; init; }
    public List<int> PhysicalDice { get; init; } = [];
}
public sealed record HitDieSpendResult
{
    public string PoolId { get; init; } = "";
    public List<int> Dice { get; init; } = [];
    public int Healing { get; init; }
}
