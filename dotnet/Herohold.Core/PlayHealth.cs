namespace Herohold.Core;

public enum HealthChangeKind { Damage, Healing, TemporaryHp }
public sealed record HealthChangeRequest:PlayRequest
{
    public HealthChangeKind Kind {get;init;}
    public int Amount {get;init;}
    public bool CriticalDamage {get;init;}
}

public sealed record AdvanceOwnTurnRequest:PlayRequest;

public static partial class PlayRules
{
    public static CharacterState AdvanceOwnTurn(WorkspaceSnapshot workspace,AdvanceOwnTurnRequest request)
    {
        if(AlreadyCompleted(workspace,request))return DomainCopy.Clone(workspace.Character);
        if(workspace.Character.Combat.EncounterActive)throw new ArgumentException("Use the combat turn controls while combat mode is active.");
        var advanced=WorkspaceRules.AdvanceTurn(workspace.Character);
        return Commit(workspace.Character,advanced,request,"Advance my turn: refresh turn abilities and tracked durations");
    }
    public static CharacterState ChangeHealth(WorkspaceSnapshot workspace,HealthChangeRequest request)
    {
        if(AlreadyCompleted(workspace,request))return DomainCopy.Clone(workspace.Character);
        if(!Enum.IsDefined(request.Kind))throw new ArgumentOutOfRangeException(nameof(request.Kind),"Choose damage, healing or temporary HP.");
        if(request.Amount is <0 or >100000 || request.Amount==0&&request.Kind!=HealthChangeKind.TemporaryHp)
            throw new ArgumentOutOfRangeException(nameof(request.Amount),"Enter an amount from 1 to 100000, or zero to clear temporary HP.");
        var before=workspace.Character;CharacterState after;
        switch(request.Kind)
        {
            case HealthChangeKind.Damage:
                var absorbed=Math.Min(before.TemporaryHp,request.Amount);
                var hpDamage=Math.Min(before.CurrentHp,request.Amount-absorbed);
                after=WorkspaceRules.AdjustHp(before with{TemporaryHp=before.TemporaryHp-absorbed},-hpDamage);
                if(!before.Sheet.DeathSaves.Dead&&after.CurrentHp==0)
                {
                    var old=before.Sheet.DeathSaves;
                    var massive=before.CurrentHp==0?request.Amount>=before.MaximumHp:request.Amount-absorbed-before.CurrentHp>=before.MaximumHp;
                    var failures=massive?3:before.CurrentHp==0?Math.Min(3,old.Failures+(request.CriticalDamage?2:1)):old.Failures;
                    after=after with{Sheet=after.Sheet with{DeathSaves=old with{Failures=failures,Stable=false,Dead=massive||failures>=3}}};
                }
                break;
            case HealthChangeKind.Healing:
                after=WorkspaceRules.AdjustHp(before,Math.Min(request.Amount,before.MaximumHp-before.CurrentHp));break;
            default:
                after=DomainCopy.Clone(before) with{TemporaryHp=request.Amount};break;
        }
        WorkspaceValidation.Character(after);
        var description=request.Kind switch{HealthChangeKind.Damage=>$"Take {request.Amount} damage",HealthChangeKind.Healing=>$"Heal {request.Amount} HP",_=>$"Set temporary HP to {request.Amount}"};
        return Commit(before,after,request,description);
    }
}
