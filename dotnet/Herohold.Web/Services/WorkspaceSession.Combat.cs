using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    public Task TransitionCombatAsync(CombatTransitionRequest request)=>MutatePlay(request,s=>PlayRules.TransitionCombat(s,request));
}
