namespace Herohold.Core;
public static partial class PlayRules
{
    public static CharacterState TransitionCombat(WorkspaceSnapshot w,CombatTransitionRequest r)
    {
        if(AlreadyCompleted(w,r))return DomainCopy.Clone(w.Character);
        var next=CombatRules.Transition(w.Character,r);
        CombatRules.Validate(next);
        return Commit(w.Character,next,r,"Combat: "+r.Kind);
    }
}
