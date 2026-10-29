using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    public Task UpdateAdventureAsync(AdventureFacts facts,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AdventureRules.SetFacts(c,facts));
    public Task AdjustCompanionResourceAsync(string companionId,string resourceId,int delta,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AdventureRules.AdjustCompanionResource(c,companionId,resourceId,delta));
    public Task AdjustCompanionHpAsync(string companionId,int delta,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AdventureRules.AdjustCompanionHp(c,companionId,delta));
    public Task AdjustItemChargesAsync(string itemId,int delta,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AdventureRules.AdjustCharges(c,itemId,delta));
}
