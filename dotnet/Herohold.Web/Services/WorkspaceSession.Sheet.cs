using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    private Task MutateSheet(string sourceCharacterId,Func<CharacterState,CharacterState> action)=>Mutate(c=>
    {
        if(string.IsNullOrWhiteSpace(sourceCharacterId)||sourceCharacterId!=c.Id)throw new WorkspaceConflictException("This editor belongs to another character. Reopen it for the selected character.");
        return action(c);
    });
    public Task UpdateProfileAsync(CharacterProfile profile,int? experiencePoints,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>SheetRules.UpdateProfile(c,profile,experiencePoints));
    public Task ChangeCoinsAsync(CoinChangeKind kind,CoinPurse amounts,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>SheetRules.ChangeCoins(c,kind,amounts));
    public Task SetHeroicInspirationAsync(bool held,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>SheetRules.SetHeroicInspiration(c,held));
    public Task SetDeathSavesAsync(DeathSaveState state,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>SheetRules.SetDeathSaves(c,state));
    public Task RecordDeathSaveAsync(int unmodifiedD20,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>SheetRules.RecordDeathSave(c,unmodifiedD20));
    public Task StabilizeAsync(string sourceCharacterId)=>MutateSheet(sourceCharacterId,SheetRules.Stabilize);
    public Task UseEquipmentAsync(string itemId,string sourceCharacterId,long? sourceRevision=null,bool confirmTableFacts=false)=>MutateSheet(sourceCharacterId,c=>
    {
        if(sourceRevision is {} revision&&Snapshot?.Revision!=revision)throw new WorkspaceConflictException("This item confirmation is stale. Reopen it before spending anything.");
        return SheetRules.UseEquipment(c,itemId,confirmTableFacts);
    });
    public Task SetAmmunitionCountingAsync(bool counting,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AmmunitionRules.SetCounting(c,counting));
    public Task RecoverAmmunitionAsync(string ammunition,string sourceCharacterId)=>MutateSheet(sourceCharacterId,c=>AmmunitionRules.Recover(c,ammunition));
    public Task UseHeroicInspirationAsync(HeroicInspirationRequest request)=>MutatePlay(request,s=>PlayRules.UseHeroicInspiration(s,request));
}
