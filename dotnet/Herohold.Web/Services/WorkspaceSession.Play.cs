using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
 public Task AdjustHitDiePoolAsync(HitDiePoolAdjustmentRequest request)=>MutatePlay(request,s=>PlayRules.AdjustHitDiePool(s,request));
 public Task AdjustHitDiePoolAsync(string id,int delta,string? sourceCharacterId=null)=>AdjustHitDiePoolAsync(new HitDiePoolAdjustmentRequest{PoolId=id,Delta=delta,SourceCharacterId=sourceCharacterId??Character?.Id??"",SourceRevision=Snapshot?.Revision??0});
 public Task ChangeHealthAsync(HealthChangeRequest request)=>MutatePlay(request,s=>PlayRules.ChangeHealth(s,request));
 public Task AdvanceOwnTurnAsync(AdvanceOwnTurnRequest request)=>MutatePlay(request,s=>PlayRules.AdvanceOwnTurn(s,request));
 public Task RollAsync(RollRequest request) => MutatePlay(request,s=>PlayRules.Roll(s,request));
 public Task ResolveRollAsync(ResolveRollRequest request) => MutatePlay(request,s=>PlayRules.ResolveRoll(s,request));
 public Task UseLuckyAsync(LuckyRequest request) => MutatePlay(request,s=>PlayRules.UseLucky(s,request));
 public Task ResolveAttackAsync(AttackResolutionRequest request) => MutatePlay(request,s=>PlayRules.ResolveAttack(s,request));
 public Task CastAsync(CastRequest request) => MutatePlay(request,s=>PlayRules.Cast(s,request));
 public Task RetargetHuntersMarkAsync(HunterMarkRetargetRequest request) => MutatePlay(request,s=>PlayRules.RetargetHuntersMark(s,request));
 public Task ResolveCastEffectAsync(CastEffectRequest request) => MutatePlay(request,s=>PlayRules.ResolveCastEffect(s,request));
 public Task RestAsync(RestRequest request) => MutatePlay(request,s=>PlayRules.Rest(s,request));
 public Task UndoAsync(UndoRequest request) => MutatePlay(request,s=>PlayRules.Undo(s,request));
 private Task AdjustHpPlayAsync(int delta,string? sourceCharacterId)
 {
  var request=new HpAdjustmentRequest{SourceCharacterId=sourceCharacterId??Snapshot?.Character.Id??"",SourceRevision=Snapshot?.Revision??0,Delta=delta};
  return MutatePlay(request,s=>PlayRules.AdjustHp(s,request));
 }
 private Task AdjustResourcePlayAsync(string id,int delta,string? sourceCharacterId)
 {
  var request=new ResourceAdjustmentRequest{SourceCharacterId=sourceCharacterId??Snapshot?.Character.Id??"",SourceRevision=Snapshot?.Revision??0,ResourceId=id,Delta=delta};
  return MutatePlay(request,s=>PlayRules.AdjustResource(s,request));
 }
 public Task SetExhaustionAsync(int level)=>Mutate(c=>WorkspaceRules.SetExhaustion(c,level));
 public Task SetDefensesAsync(List<DamageDefense> defenses)=>Mutate(c=>WorkspaceRules.SetDefenses(c,defenses));
 public Task SetKnownTargetAsync(KnownTargetState target)=>Mutate(c=>WorkspaceRules.SetKnownTarget(c,target));
 public Task AddEquipmentAsync(EquipmentItem item)=>MutateEquipment(c=>WorkspaceRules.AddEquipment(c,item));
 public Task UpdateEquipmentAsync(EquipmentItem item)=>MutateEquipment(c=>WorkspaceRules.UpdateEquipment(c,item));
 public Task RemoveEquipmentAsync(string id)=>MutateEquipment(c=>WorkspaceRules.RemoveEquipment(c,id));
 private async Task MutateEquipment(Func<CharacterState,CharacterState> change)
 {
  var expectedId=Snapshot?.Character.Id;
  await gate.WaitAsync();
  try
  {
   Begin("Saving…");
   if(Snapshot is null)throw new InvalidOperationException("Load a character first.");
   if(expectedId!=Snapshot.Character.Id)throw new WorkspaceConflictException("This inventory change belongs to the previously opened character.");
   var candidate=change(Snapshot.Character);
   if(candidate.Build is not null&&rules is not null)
   {
    var changed=candidate;var projected=await RunRules(r=>r.Recalculate(CharacterDraft.Of(changed),changed));
    candidate=projected with{CurrentHp=Math.Min(candidate.CurrentHp,projected.MaximumHp),TemporaryHp=candidate.TemporaryHp,Play=projected.Play with{UndoHistory=[],LastRoll=null,LastCast=null,CompletedOperationIds=[]}};
   }
   candidate=SheetRules.CloseInspirationWindows(candidate);
   Snapshot=await repository.SaveAsync(Snapshot with{Character=candidate},Snapshot.Revision);Saved();
  }
  catch(Exception error)when(IsExpected(error)){Failed(error);}
  finally{Finish();gate.Release();}
 }
 private async Task MutatePlay(PlayRequest request,Func<WorkspaceSnapshot,CharacterState> action)
 {
  await gate.WaitAsync();
  try
  {
   Begin("Saving…");
   if(Snapshot is null)throw new InvalidOperationException("Load a character first.");
   if(!string.IsNullOrEmpty(request.SourceCharacterId)&&request.SourceCharacterId!=Snapshot.Character.Id)throw new WorkspaceConflictException("This action belongs to another character. Reopen its controls before trying again.");
   if(Snapshot.Character.Play.CompletedOperationIds.Contains(request.OperationId)){Saved();return;}
   var candidate=Snapshot with{Character=action(Snapshot)};
   Snapshot=await repository.SaveAsync(candidate,Snapshot.Revision);Saved();
  }
  catch(Exception error)when(IsExpected(error)){Failed(error);}
  finally{Finish();gate.Release();}
 }
}
