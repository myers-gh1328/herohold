namespace Herohold.Core;

public static class CombatRules
{
    public static CombatActionContext Context(CharacterState c,string actionId,IEnumerable<string>? armed=null)=>new(){EncounterId=c.Combat.EncounterId,TurnId=c.Combat.TurnId,ActionId=actionId,CastId=c.Play.LastCast is {} cast&&actionId=="spell:"+cast.Preview.SpellId?cast.Id:"",ArmedEffects=armed?.ToList()??[]};
    public static CharacterState PrepareDeathSave(CharacterState c)
    {
        if(c.Combat.EncounterActive&&(!c.Combat.Enabled||c.Combat.Phase!=CombatPhase.OwnTurn||c.Combat.TurnId.Length==0||c.Combat.DeathSaveRecordedTurnId==c.Combat.TurnId))throw new ArgumentException("Record one death save at the start of your own active turn; this turn's save is already recorded or unavailable.");
        return c;
    }
    public static CharacterState FinalizeDeathSave(CharacterState c)=>!c.Combat.EncounterActive?c:c with{Combat=c.Combat with{DeathSaveRecordedTurnId=c.Combat.TurnId}};
    public static CharacterState PrepareRetarget(CharacterState c,HunterMarkRetargetRequest r)
    {
        if(!c.Combat.EncounterActive)return DomainCopy.Clone(c);
        var x=CheckContext(c,r);
        if(x.ActionId!="spell:hunters-mark-retarget"||c.Combat.Phase!=CombatPhase.OwnTurn||!c.Combat.BonusActionAvailable)throw new ArgumentException("Moving Hunter’s Mark requires an available Bonus Action on your own turn.");
        return c with{Combat=c.Combat with{BonusActionAvailable=false}};
    }
    public static CombatOption CastOption(CharacterState c,SpellDefinition spell)
    {
        var cost=spell.CastingTime.Contains("Reaction",StringComparison.OrdinalIgnoreCase)?CombatCost.Reaction:spell.CastingTime.Contains("Bonus",StringComparison.OrdinalIgnoreCase)?CombatCost.BonusAction:CombatCost.Action;
        try
        {
            var owner=ClassPlayRules.SpellSource(c,spell.Id);var view=owner is null?c:ClassPlayRules.SourceView(c,owner.Id);
            if(!view.Play.PreparedSpellIds.Concat(view.Play.KnownSpellIds).Concat(view.Play.GrantedSpellIds).Contains(spell.Id))throw new ArgumentException("This spell is not prepared, known or granted for combat casting.");
            var pool=c.Resources.FirstOrDefault(p=>p.Remaining>0&&((spell.GrantResourceId==p.Id&&view.Play.GrantedSpellIds.Contains(spell.Id))||(spell.Id=="hunters-mark"&&p.Id=="hunters")));
            pool??=c.Resources.Where(p=>p.Remaining>0&&spell.AllowsSlotCasting&&(p.SlotLevel??(p.Id.StartsWith("slot",StringComparison.Ordinal)&&int.TryParse(p.Id.AsSpan(4),out var n)?n:0))>=spell.Level).OrderBy(p=>p.SlotLevel).FirstOrDefault();
            var free=spell.Level==0||spell.AtWill&&view.Play.GrantedSpellIds.Contains(spell.Id);
            if(!free&&pool is null)throw new ArgumentException("No eligible spell slot or free casting use remains.");
            var ctx=Context(c,"spell:"+spell.Id) with{TriggerConfirmed=true};
            PrepareCast(c,new(){SpellId=spell.Id,SlotLevel=pool?.SlotLevel??spell.Level,PaymentResourceId=free?"none":pool!.Id,CombatContext=ctx});
            return new("spell:"+spell.Id,spell.Name,cost,true,cost==CombatCost.Reaction?CombatSupport.NeedsTableFact:CombatSupport.Supported,cost==CombatCost.Reaction?"Confirm the spell's reaction trigger before casting.":"",spell.Source);
        }
        catch(ArgumentException ex){return new("spell:"+spell.Id,spell.Name,cost,false,CombatSupport.Supported,ex.Message,spell.Source);}
    }
    public static CombatOption AttackOption(CharacterState c,string key,AttackKind kind)
    {
        try{PrepareRoll(c,new(){Kind=RollKind.Attack,AttackKind=kind,CombatContext=Context(c,"attack:"+key)});return new("attack:"+key,key,CombatCost.Attack,true,CombatSupport.Supported,"");}
        catch(ArgumentException ex){return new("attack:"+key,key,CombatCost.Attack,false,CombatSupport.Supported,ex.Message);}
    }
    public static CombatOption ReactionAttackOption(CharacterState c,string key,AttackKind kind)
    {
        try{PrepareRoll(c,new(){Kind=RollKind.Attack,AttackKind=kind,CombatContext=Context(c,"attack:"+key) with{TriggerConfirmed=true,ConfirmedFacts=["reaction-attack"]}});return new("attack:"+key,key,CombatCost.Reaction,true,CombatSupport.NeedsTableFact,"Confirm the actual reaction-attack trigger before rolling.");}
        catch(ArgumentException ex){return new("attack:"+key,key,CombatCost.Reaction,false,CombatSupport.NeedsTableFact,ex.Message);}
    }
    public static CharacterState FinalizeCast(CharacterState c,CastRequest r)
    {
        if(c.Play.LastCast is {} committed)c=c with{Play=c.Play with{LastCast=committed with{AttacksRemaining=committed.Preview.AttackRollCount}}};
        if(!c.Combat.Enabled)return c;
        var spell=SpellCatalog.Find(c,r.SpellId);var timers=c.Combat.Timers.Where(t=>c.Effects.Any(e=>e.Id==t.EffectId)).ToList();
        var preview=SpellRules.Preview(c,r.SpellId,r.SlotLevel);
        var source=preview.Resolution=="marked-attack-hit"?"spell:hunters-mark":"spell:"+r.SpellId;
        foreach(var effect in c.Effects.Where(e=>e.Source==source&&!timers.Any(t=>t.EffectId==e.Id)))
        {
            var amount=spell.DurationAmount;var unit=spell.DurationUnit;
            // Thresholds preserve the source unit; do not derive an anchor from old RemainingRounds.
            var threshold=spell.DurationThresholds.Where(x=>x.MinimumSlotLevel<=r.SlotLevel).OrderBy(x=>x.MinimumSlotLevel).LastOrDefault();
            if(threshold is not null){amount=threshold.Amount;unit=threshold.Unit;}
            var seconds=unit.ToLowerInvariant() switch{"second" or "seconds"=>1,"minute" or "minutes"=>60,"hour" or "hours"=>3600,"day" or "days"=>86400,_=>0};
            if(amount>0&&(seconds>0||unit is "round" or "rounds"))timers.Add(new(){EffectId=effect.Id,CreatorId=c.Id,Duration=seconds>0?CombatDuration.GameSeconds:CombatDuration.Rounds,OriginalUnit=unit,OriginalAmount=amount,Remaining=checked(amount*(seconds>0?seconds:1)),CreatedSequence=c.Combat.Sequence});
        }
        return c with{Combat=c.Combat with{Timers=timers}};
    }
    private static CombatActionContext CheckContext(CharacterState c,PlayRequest r)
    {
        if(!c.Combat.Enabled)throw new ArgumentException("Resume combat mode or end the active encounter before taking tracked actions.");
        var x=r.CombatContext;
        if(!c.Combat.EncounterActive||x is null||x.EncounterId!=c.Combat.EncounterId||x.TurnId!=c.Combat.TurnId||string.IsNullOrWhiteSpace(x.ActionId))throw new WorkspaceConflictException("Reopen this action for the current encounter and creature turn.");
        if(string.IsNullOrWhiteSpace(c.Combat.TurnId))throw new ArgumentException("Record whose creature turn is active before taking this combat action.");
        if(Incapacitated(c))throw new ArgumentException("Incapacitated: actions and reactions are unavailable.");
        return x;
    }
    public static CharacterState PrepareRoll(CharacterState c,RollRequest r)
    {
        if(r.Kind==RollKind.SpellAttack&&(c.Combat.EncounterActive||r.CombatContext?.CastId.Length>0))return PrepareSpellAttack(c,r);
        if(!c.Combat.EncounterActive)return c;
        if(c.Play.LastRoll is {Resolved:false})throw new ArgumentException("Resolve or explicitly abandon your previous roll before starting another.");
        if(r.Kind is not(RollKind.Attack or RollKind.SpellAttack))return c;
        var x=CheckContext(c,r);
        if(!x.ActionId.StartsWith("attack:",StringComparison.Ordinal))throw new ArgumentException("Choose a recorded attack source.");
        var key=x.ActionId[7..];
        if(r.AttackKind==AttackKind.Unknown)throw new ArgumentException("This attack source needs table review before automated combat use.");
        if(r.AttackKind==AttackKind.Weapon&&!c.Equipment.Any(e=>e.Quantity>0&&e.Equipped&&(e.Id.Equals(key,StringComparison.OrdinalIgnoreCase)||e.Id.Equals("weapon:"+key,StringComparison.OrdinalIgnoreCase)||e.Name.Replace(" ","-").Equals(key,StringComparison.OrdinalIgnoreCase))))throw new ArgumentException("Equip the recorded weapon before attacking.");
        if(x.ArmedEffects.Any(e=>e is not("hunters-mark" or "dreadful-strike"))||x.ArmedEffects.Count!=x.ArmedEffects.Distinct().Count())throw new ArgumentException("Choose supported, unique pre-roll effects.");
        if(x.ArmedEffects.Contains("hunters-mark")&&(!c.Effects.Any(e=>e.Concentration&&e.Source=="spell:hunters-mark")||string.IsNullOrWhiteSpace(c.Play.HunterTarget)))throw new ArgumentException("Hunter’s Mark requires an active marked target and concentration.");
        if(x.ArmedEffects.Contains("dreadful-strike")&&(r.AttackKind!=AttackKind.Weapon||RangerRules.ClassLevel(c)<3||!RangerRules.IsGloomStalker(c)||c.Play.DreadUsedThisTurn||!c.Resources.Any(p=>p.Id=="dread"&&p.Remaining>0)))throw new ArgumentException("Dreadful Strike is unavailable for this attack.");
        if(x.ConfirmedFacts.Contains("reaction-attack"))
        {
            if(!x.TriggerConfirmed||!c.Combat.ReactionAvailable)throw new ArgumentException("Confirm an available reaction and its attack trigger.");
            return c with{Combat=c.Combat with{ReactionAvailable=false}};
        }
        if(c.Combat.Phase!=CombatPhase.OwnTurn)throw new ArgumentException("An Attack action is available only on your own turn.");
        var after=c;
        if(after.Combat.AttacksRemaining==0)after=Spend(after,"attack");
        return after with{Combat=after.Combat with{AttacksRemaining=after.Combat.AttacksRemaining-1}};
    }
    private static CharacterState PrepareSpellAttack(CharacterState c,RollRequest r)
    {
        if(c.Play.LastRoll is {Resolved:false})throw new ArgumentException("Resolve the previous attack before rolling another beam or ray.");
        var cast=c.Play.LastCast;var ctx=r.CombatContext;
        if(cast is null||ctx is null||ctx.CastId!=cast.Id||ctx.ActionId!="spell:"+cast.Preview.SpellId||cast.Preview.AttackRollCount is null or<=0||cast.AttacksRemaining is null or<=0||cast.AttacksRemaining>cast.Preview.AttackRollCount)throw new ArgumentException("No finite attack remains for this casting receipt. Resolve unknown attack sequences at the table.");
        if(r.Mode!=cast.Mode)throw new ArgumentException("Keep the dice mode captured by this casting receipt for each attack and its damage.");
        if(c.Combat.EncounterActive)CheckContext(c,r);
        var spell=SpellCatalog.Find(c,cast.Preview.SpellId);
        if(r.Ability!=(spell.CastingAbility??c.Play.SpellcastingAbility))throw new ArgumentException("Use this spell source's casting ability for its attack.");
        if(Incapacitated(c))throw new ArgumentException("Incapacitated: this spell attack is unavailable.");
        if(ctx.ArmedEffects.Any(e=>e!="hunters-mark")||ctx.ArmedEffects.Distinct().Count()!=ctx.ArmedEffects.Count)throw new ArgumentException("Only eligible Hunter’s Mark damage can be armed for this spell attack; Dreadful Strike requires a weapon.");
        if(ctx.ArmedEffects.Contains("hunters-mark")&&(!c.Effects.Any(e=>e.Concentration&&e.Source=="spell:hunters-mark")||string.IsNullOrWhiteSpace(c.Play.HunterTarget)))throw new ArgumentException("Hunter’s Mark requires an active quarry and concentration.");
        return c with{Play=c.Play with{LastCast=cast with{AttacksRemaining=cast.AttacksRemaining-1}}};
    }
    public static CharacterState PrepareCast(CharacterState c,CastRequest r)
    {
        if(!c.Combat.EncounterActive)return c;
        if(HasPendingCast(c))throw new ArgumentException("Resolve or explicitly abandon your previous spell effect before casting another spell.");
        var x=CheckContext(c,r);var spell=SpellCatalog.Find(c,r.SpellId);var s=c.Combat;
        if(x.ActionId!="spell:"+r.SpellId)throw new ArgumentException("The selected spell does not match this combat action.");
        if(r.Ritual)throw new ArgumentException("Ritual casting takes additional game time; leave combat mode to manage it at the table.");
        var timing=spell.CastingTime.Trim();
        var slot=r.SlotLevel>0&&r.PaymentResourceId!="none"&&r.PaymentResourceId!="hunters-mark"&&c.Resources.Any(p=>p.Id==r.PaymentResourceId&&(p.SlotLevel is not null||p.Id.StartsWith("slot",StringComparison.Ordinal)));
        if(slot&&s.SlotCastThisTurn)throw new ArgumentException("You already expended a spell slot to cast a spell this creature turn.");
        // A requested slot pool is checked by the casting transaction after this budget stage.
        slot|=r.SlotLevel>0&&r.PaymentResourceId.StartsWith("slot",StringComparison.Ordinal);
        if(slot&&s.SlotCastThisTurn)throw new ArgumentException("You already expended a spell slot to cast a spell this creature turn.");
        if(timing.Contains("Reaction",StringComparison.OrdinalIgnoreCase))
        {
            if(!s.ReactionAvailable||!x.TriggerConfirmed)throw new ArgumentException("Confirm the spell's reaction trigger and an available Reaction.");
            s=s with{ReactionAvailable=false};
        }
        else
        {
            if(s.Phase!=CombatPhase.OwnTurn)throw new ArgumentException("Wait for your own turn to cast this spell.");
            if(timing.Contains("Bonus",StringComparison.OrdinalIgnoreCase))
            {if(!s.BonusActionAvailable)throw new ArgumentException("Your Bonus Action has been spent.");s=s with{BonusActionAvailable=false};}
            else if(timing.Equals("Action",StringComparison.OrdinalIgnoreCase)||timing.Equals("1 action",StringComparison.OrdinalIgnoreCase))
            {if(!s.ActionAvailable)throw new ArgumentException("Your Action has been spent. Additional restricted actions cannot cast spells.");s=s with{ActionAvailable=false};}
            else throw new ArgumentException("This casting time requires table-managed game time; it is not a single combat action.");
        }
        return c with{Combat=s with{SlotCastThisTurn=s.SlotCastThisTurn||slot}};
    }
    public static CharacterState FinalizeRoll(CharacterState c,RollRequest r)
    {
        if(r.Kind is not(RollKind.Attack or RollKind.SpellAttack))return c;
        var profile=r.CombatContext?.AttackProfile;
        if(r.Kind==RollKind.SpellAttack&&c.Play.LastCast is {} cast&&r.CombatContext?.CastId==cast.Id&&cast.Preview.AttackRollCount is>0)
            profile=cast.Preview.Dice is {} dice?new(){Name=cast.Preview.Name,Dice=dice,Modifier=cast.Preview.Modifier,DamageType=cast.Preview.DamageType??""}:null;
        if(!c.Combat.Enabled&&profile is null)return c;
        return c with{Combat=c.Combat with{AttackSelection=new(){CastId=r.CombatContext?.CastId??"",AttackProfile=profile,RollId=c.Play.LastRoll?.Id??"",Target=c.Play.HunterTarget,MarkEffectId=c.Effects.FirstOrDefault(e=>e.Concentration&&e.Source=="spell:hunters-mark")?.Id??"",ArmedEffects=[..r.CombatContext?.ArmedEffects??[]]}}};
    }
    public static void ValidateHit(CharacterState c,AttackResolutionRequest r)
    {
        var spellAttack=c.Play.LastRoll?.Kind==RollKind.SpellAttack;
        if(!c.Combat.EncounterActive&&!spellAttack)return;
        if(c.Combat.EncounterActive&&!c.Combat.Enabled)throw new ArgumentException("Resume combat mode before resolving this attack.");
        var selection=c.Combat.AttackSelection;
        if(selection is null||selection.RollId!=r.RollId)throw new WorkspaceConflictException("This hit belongs to another combat roll. Reopen the current attack.");
        if(spellAttack)
        {
            var profile=selection.AttackProfile;
            if(profile?.Dice is null)throw new ArgumentException("This spell attack has no supported per-hit damage; resolve it at the table.");
            if(r.Hit&&(r.BaseDamage!=profile.Dice||r.DamageModifier!=profile.Modifier||!r.DamageType.Equals(profile.DamageType,StringComparison.OrdinalIgnoreCase)||r.BaseFixedDamage is not null))throw new ArgumentException("Use the per-hit damage captured from this spell's casting receipt.");
        }
        if(r.HuntersMark&&!selection.ArmedEffects.Contains("hunters-mark")||r.DreadfulStrike&&!selection.ArmedEffects.Contains("dreadful-strike"))throw new ArgumentException("Choose this on-hit effect before the attack roll; unarmed effects cannot be added afterward.");
        if(r.HuntersMark&&(string.IsNullOrWhiteSpace(r.Target)||!r.Target.Equals(selection.Target,StringComparison.OrdinalIgnoreCase)||!c.Play.HunterTarget.Equals(selection.Target,StringComparison.OrdinalIgnoreCase)||!c.Effects.Any(e=>e.Id==selection.MarkEffectId&&e.Concentration&&e.Source=="spell:hunters-mark")))throw new ArgumentException("This attack must hit the quarry and mark source recorded before the roll.");
    }
    public static CharacterState Transition(CharacterState character,CombatTransitionRequest request)
    {
        var c=DomainCopy.Clone(character);var s=c.Combat;var r=request;
        if(r.Kind is CombatTransitionKind.StartTurn or CombatTransitionKind.EndTurn or CombatTransitionKind.ExternalTurn or CombatTransitionKind.AdvanceRound or CombatTransitionKind.AdvanceGameTime or CombatTransitionKind.EndEncounter)
        {
            var pendingRoll=c.Play.LastRoll is {Resolved:false};
            var pendingCast=HasPendingCast(c);
            if(pendingRoll||pendingCast)
            {
                if(!r.AbandonPending)throw new ArgumentException("Resolve the pending roll or spell effect, or explicitly abandon it before advancing combat time.");
                c=c with{Play=c.Play with{LastRoll=c.Play.LastRoll is {} roll?roll with{Resolved=true}:null,LastCast=pendingCast?null:c.Play.LastCast}};
            }
        }
        if(r.Kind==CombatTransitionKind.Enter)
        {
            if(s.EncounterActive)throw new ArgumentException("Resume this encounter or end it before starting another.");
            WorkspaceValidation.Id(r.EncounterId);
            s=new(){Enabled=true,EncounterActive=true,EncounterId=r.EncounterId,Round=1,Phase=CombatPhase.Unknown,Layout=s.Layout,Timers=s.Timers,ReactionAvailable=s.ReactionAvailable,Sequence=s.Sequence};
            if(r.OwnTurnNow)
            {
                if(!r.TableBoundaryConfirmed)throw new ArgumentException("Confirm with the table that your turn is starting.");
                WorkspaceValidation.Id(r.BoundaryId);
                s=s with{Phase=CombatPhase.OwnTurn,TurnId=r.BoundaryId,ActorId=c.Id,Sequence=s.Sequence+1,OwnTurnNumber=s.OwnTurnNumber+1,ActionAvailable=true,BonusActionAvailable=true,ReactionAvailable=true,CompletedBoundaryIds=[..s.CompletedBoundaryIds,r.BoundaryId]};
            }
            c=c with{Combat=s,Turn=r.OwnTurnNow?c.Turn+1:c.Turn,Play=r.OwnTurnNow?c.Play with{DreadUsedThisTurn=false}:c.Play};return r.OwnTurnNow?Tick(c,CombatBoundary.OwnStart,c.Id,1):c;
        }
        if(!s.EncounterActive)throw new ArgumentException("Start an encounter first.");
        if(r.EncounterId.Length>0&&r.EncounterId!=s.EncounterId)throw new WorkspaceConflictException("This action belongs to another encounter.");
        if(r.Kind==CombatTransitionKind.Resume)return c with{Combat=s with{Enabled=true}};
        if(r.Kind==CombatTransitionKind.Leave)return c with{Combat=s with{Enabled=false}};
        if(!s.Enabled)throw new ArgumentException("Resume combat mode first.");
        switch(r.Kind)
        {
            case CombatTransitionKind.EndEncounter:
                return c with{Combat=s with{Enabled=false,EncounterActive=false,Phase=CombatPhase.Unknown,PendingOwnTurnBoundaryId="",AttackSelection=null}};
            case CombatTransitionKind.ConfirmOwnTurn:
                if(s.Phase==CombatPhase.OwnTurn)throw new ArgumentException("End your current turn first.");
                if(!r.TableBoundaryConfirmed)throw new ArgumentException("Confirm the new turn boundary with the table.");
                WorkspaceValidation.Id(r.BoundaryId);
                if(s.CompletedBoundaryIds.Contains(r.BoundaryId))throw new ArgumentException("This turn boundary has already been used.");
                return c with{Combat=s with{PendingOwnTurnBoundaryId=r.BoundaryId}};
            case CombatTransitionKind.StartTurn:
                if(r.TableBoundaryConfirmed&&s.Phase!=CombatPhase.OwnTurn)
                {
                    WorkspaceValidation.Id(r.BoundaryId);
                    if(s.CompletedBoundaryIds.Contains(r.BoundaryId))throw new ArgumentException("This turn boundary has already been used.");
                    s=s with{PendingOwnTurnBoundaryId=r.BoundaryId};
                }
                if(s.Phase==CombatPhase.OwnTurn||s.PendingOwnTurnBoundaryId.Length==0)throw new ArgumentException("Confirm a new own-turn boundary before starting.");
                if(s.Phase==CombatPhase.ExternalTurn)
                {
                    c=Tick(c,CombatBoundary.ActorEnd,s.ActorId,1);
                    s=c.Combat with{PendingOwnTurnBoundaryId=s.PendingOwnTurnBoundaryId};
                }
                s=s with{Phase=CombatPhase.OwnTurn,TurnId=s.PendingOwnTurnBoundaryId,PendingOwnTurnBoundaryId="",ActorId=c.Id,Sequence=s.Sequence+1,OwnTurnNumber=s.OwnTurnNumber+1,ActionAvailable=true,BonusActionAvailable=true,ReactionAvailable=true,AttacksRemaining=0,MovementSpent=0,MovementGranted=0,SlotCastThisTurn=false,AdditionalActions=[],CompletedBoundaryIds=[..s.CompletedBoundaryIds.Append(s.PendingOwnTurnBoundaryId).TakeLast(100)],DeathSavesAtTurnStart=c.CurrentHp==0?c.Sheet.DeathSaves:null};
                c=c with{Turn=c.Turn+1,Play=c.Play with{DreadUsedThisTurn=false},Combat=s};
                return Tick(c,CombatBoundary.OwnStart,c.Id,1);
            case CombatTransitionKind.EndTurn:
                if(s.Phase!=CombatPhase.OwnTurn)throw new ArgumentException("Your turn is not active.");
                if(Preview(c,r).RequiresConfirmation&&!r.AdvanceConfirmed)throw new ArgumentException("Confirm ending your turn with unused usable actions.");
                c=Tick(c,CombatBoundary.OwnEnd,c.Id,1);
                return c with{Combat=c.Combat with{Phase=CombatPhase.BetweenTurns,ActionAvailable=false,BonusActionAvailable=false,AttacksRemaining=0,PendingOwnTurnBoundaryId=""}};
            case CombatTransitionKind.ExternalTurn:
                if(s.Phase==CombatPhase.OwnTurn)throw new ArgumentException("End your own turn first.");
                if(!r.TableBoundaryConfirmed)throw new ArgumentException("Confirm the creature's turn with the table.");
                WorkspaceValidation.Id(r.ActorId);WorkspaceValidation.Id(r.BoundaryId);
                if(s.CompletedBoundaryIds.Contains(r.BoundaryId))throw new ArgumentException("This turn boundary has already been used.");
                if(s.Phase==CombatPhase.ExternalTurn)c=Tick(c,CombatBoundary.ActorEnd,s.ActorId,1);
                c=c with{Combat=c.Combat with{Phase=CombatPhase.ExternalTurn,ActorId=r.ActorId,TurnId=r.BoundaryId,SlotCastThisTurn=false,Sequence=s.Sequence+1,CompletedBoundaryIds=[..s.CompletedBoundaryIds.Append(r.BoundaryId).TakeLast(100)]},Play=c.Play with{DreadUsedThisTurn=false}};
                return Tick(c,CombatBoundary.ActorStart,r.ActorId,1);
            case CombatTransitionKind.AdvanceRound:
                if(r.Round<=s.Round||r.Round>s.Round+100)throw new ArgumentException("Choose a later round, at most 100 rounds ahead.");
                if(Preview(c,r).RequiresConfirmation&&!r.AdvanceConfirmed)throw new ArgumentException("Review skipped rounds and pending saves first.");
                c=Tick(c,CombatBoundary.Round,"",r.Round-s.Round);
                return c with{Combat=c.Combat with{Round=r.Round}};
            case CombatTransitionKind.AdvanceGameTime:
                if(r.Amount<1||r.Amount>86400)throw new ArgumentException("Advance between 1 and 86400 game seconds.");
                return Tick(c,CombatBoundary.GameTime,"",r.Amount);
            case CombatTransitionKind.SpendAction:
                return Spend(c,r.ActionId,r.TableBoundaryConfirmed);
            case CombatTransitionKind.Move:
                if(s.Phase!=CombatPhase.OwnTurn||Incapacitated(c)||SheetRules.EffectiveEffects(c).Any(e=>e.Name.Equals("Grappled",StringComparison.OrdinalIgnoreCase)||e.Name.Equals("Restrained",StringComparison.OrdinalIgnoreCase))||r.Amount<0||s.MovementSpent+r.Amount>CharacterCalculations.Speed(c)+s.MovementGranted)throw new ArgumentException("Movement exceeds your available movement or is prevented by a tracked condition.");
                return c with{Combat=s with{MovementSpent=s.MovementSpent+r.Amount}};
            case CombatTransitionKind.AddEffect:
                if(r.Effect is null||r.Timer is null||r.Timer.EffectId!=r.Effect.Id)throw new ArgumentException("Choose an effect and its matching timer.");
                WorkspaceValidation.Id(r.Effect.Id);
                var existing=c.Effects.FirstOrDefault(x=>x.Id==r.Effect.Id);
                if(existing is not null&&existing!=r.Effect)throw new ArgumentException("An existing effect's timer can be changed without replacing its source.");
                if(r.Timer.Remaining<0||!Enum.IsDefined(r.Timer.Duration))throw new ArgumentException("Choose a valid timer.");
                return c with{Effects=existing is null?[..c.Effects,r.Effect]:c.Effects,Combat=s with{Timers=[..s.Timers.Where(t=>t.EffectId!=r.Effect.Id),r.Timer with{CreatedSequence=s.Sequence}]}};
            case CombatTransitionKind.ResolveSave:
                if(!s.Timers.Any(x=>x.EffectId==r.EffectId&&x.AwaitingSave))throw new ArgumentException("There is no pending save for this effect.");
                return c with{Effects=r.SaveSucceeded?c.Effects.Where(x=>x.Id!=r.EffectId).ToList():c.Effects,Combat=s with{Timers=r.SaveSucceeded?s.Timers.Where(x=>x.EffectId!=r.EffectId).ToList():s.Timers.Select(x=>x.EffectId==r.EffectId?x with{AwaitingSave=false}:x).ToList()}};
            case CombatTransitionKind.ConfigureLayout:
                if(r.Layout is null)throw new ArgumentException("Choose a combat layout.");
                ValidateLayout(r.Layout);return c with{Combat=s with{Layout=r.Layout}};
            default:throw new ArgumentException("Unknown combat transition.");
        }
    }
    public static CombatAdvancePreview Preview(CharacterState c,CombatTransitionRequest r)
    {
        var unused=new List<string>();
        if(r.Kind==CombatTransitionKind.EndTurn&&c.Combat.Phase==CombatPhase.OwnTurn&&!Incapacitated(c))
        {
            if(c.Combat.ActionAvailable)unused.Add("Action");
            if(c.Combat.AttacksRemaining>0)unused.Add($"{c.Combat.AttacksRemaining} attack opportunities");
            if(c.Combat.BonusActionAvailable&&HasBonusOption(c))unused.Add("Bonus Action");
        }
        return new(unused,c.Combat.Timers.Where(x=>x.Duration==CombatDuration.Rounds&&r.Kind==CombatTransitionKind.AdvanceRound&&x.Remaining<=r.Round-c.Combat.Round).Select(x=>c.Effects.FirstOrDefault(e=>e.Id==x.EffectId)?.Name??x.EffectId).ToList(),c.Combat.Timers.Where(x=>x.AwaitingSave).Select(x=>x.SavePrompt).ToList(),r.Kind==CombatTransitionKind.AdvanceRound?Math.Max(0,r.Round-c.Combat.Round-1):0);
    }
    public static bool Incapacitated(CharacterState c)=>c.CurrentHp<=0||c.Exhaustion>=6||SheetRules.EffectiveEffects(c).Any(e=>new[]{"incapacitated","paralyzed","petrified","stunned","unconscious"}.Contains(e.Name.Trim().ToLowerInvariant()));
    private static bool HasBonusOption(CharacterState c)=>c.Play.PreparedSpellIds.Concat(c.Play.KnownSpellIds).Concat(c.Play.GrantedSpellIds).Concat(c.Play.SpellSources.SelectMany(s=>s.PreparedSpellIds.Concat(s.KnownSpellIds).Concat(s.GrantedSpellIds))).Distinct().Any(id=>{try{var spell=SpellCatalog.Find(c,id);return spell.CastingTime.Contains("Bonus",StringComparison.OrdinalIgnoreCase)&&CastOption(c,spell).Allowed;}catch(ArgumentException){return false;}});
    public static IReadOnlyList<CombatOption> Options(CharacterState c)
    {
        var why=!c.Combat.Enabled?"Combat mode is off.":Incapacitated(c)?"Incapacitated: you cannot take actions or reactions.":c.Combat.Phase!=CombatPhase.OwnTurn?"Wait for your own turn.":!c.Combat.ActionAvailable?"Your Action has been spent.":"";
        var list=new List<CombatOption>();
        foreach(var id in new[]{"attack","dash","disengage","dodge","help","hide","influence","search","study","utilize","ready"}){var grant=c.Combat.AdditionalActions.Any(g=>g.Remaining>0&&g.PermittedActions.Contains(id));var reason=why=="Your Action has been spent."&&grant?"":why;list.Add(new(id,char.ToUpperInvariant(id[0])+id[1..],CombatCost.Action,reason.Length==0,id is "help" or "hide" or "influence" or "utilize" or "ready"?CombatSupport.NeedsTableFact:CombatSupport.Supported,reason));}
        if(ClassLevel(c,"Fighter")>=2&&c.Resources.Any(p=>p.Id=="action-surge"))list.Add(new("action-surge","Action Surge",CombatCost.None,c.Combat.Enabled&&c.Combat.Phase==CombatPhase.OwnTurn&&!Incapacitated(c)&&c.Resources.Any(p=>p.Id=="action-surge"&&p.Remaining>0)&&!c.Combat.AdditionalActions.Any(g=>g.Id=="action-surge"),CombatSupport.Supported,"One additional action, except the Magic action. Once per turn.","2024 Fighter: Action Surge"));
        var reactionWhy=!c.Combat.Enabled?"Combat mode is off.":Incapacitated(c)?"Incapacitated: reactions are unavailable.":string.IsNullOrWhiteSpace(c.Combat.TurnId)?"Record the current creature turn first.":!c.Combat.ReactionAvailable?"Reaction spent until the start of your next turn.":"Confirm a valid reaction trigger with the table.";
        list.Add(new("reaction","Triggered reaction",CombatCost.Reaction,c.Combat.Enabled&&!Incapacitated(c)&&c.Combat.ReactionAvailable&&!string.IsNullOrWhiteSpace(c.Combat.TurnId),CombatSupport.NeedsTableFact,reactionWhy));
        return list;
    }
    public static CharacterState Spend(CharacterState c,string id,bool confirmed=false)
    {
        var option=Options(c).FirstOrDefault(x=>x.Id==id)??throw new ArgumentException("This action requires source-specific support; it cannot be validated automatically.");
        if(!option.Allowed)throw new ArgumentException(option.Reason);
        if(option.Support==CombatSupport.NeedsTableFact&&!confirmed)throw new ArgumentException("Confirm the trigger and required table facts first.");
        var s=c.Combat;
        if(id=="action-surge")return c with{Resources=c.Resources.Select(p=>p.Id=="action-surge"?p with{Remaining=p.Remaining-1}:p).ToList(),Combat=s with{AdditionalActions=[..s.AdditionalActions,new(){Id="action-surge",Source="2024 Fighter: Action Surge",Remaining=1,PermittedActions=["attack","dash","disengage","dodge","help","hide","influence","search","study","utilize","ready"]}]}};
        if(option.Cost==CombatCost.Reaction){var effects=c.Effects.Where(e=>e.Source!="combat:ready").ToList();return c with{Effects=effects,Combat=s with{ReactionAvailable=false,Timers=s.Timers.Where(t=>effects.Any(e=>e.Id==t.EffectId)).ToList()}};}
        var grants=s.AdditionalActions.ToList();if(!s.ActionAvailable){var index=grants.FindIndex(g=>g.Remaining>0&&g.PermittedActions.Contains(id));if(index<0)throw new ArgumentException("No eligible action allowance remains.");grants[index]=grants[index] with{Remaining=grants[index].Remaining-1};}
        var after=c with{Combat=s with{ActionAvailable=false,AdditionalActions=grants,AttacksRemaining=id=="attack"?AttackCount(c):s.AttacksRemaining,MovementGranted=id=="dash"?s.MovementGranted+CharacterCalculations.Speed(c):s.MovementGranted}};
        if(id=="ready"){var effect=new ActiveEffect{Id=Guid.NewGuid().ToString("N"),Name="Readied nonspell action",Source="combat:ready",Note="Resolve the declared perceivable trigger at the table; taking the readied action costs your Reaction. Expires at start of your next turn. Readied spells require separate source-specific support."};after=after with{Effects=[..after.Effects,effect],Combat=after.Combat with{Timers=[..after.Combat.Timers,new(){EffectId=effect.Id,CreatorId=c.Id,Duration=CombatDuration.CreatorNextStart,CreatedSequence=s.Sequence}]}};}
        return after;
    }
    private static int ClassLevel(CharacterState c,string name)=>c.Build?.ClassLevels is {} levels?levels.FirstOrDefault(x=>x.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Value:c.ClassName.Equals(name,StringComparison.OrdinalIgnoreCase)?c.Level:0;
    private static int AttackCount(CharacterState c)
    {
        if(c.Derived.AttackActionCount is {} count)return count;
        int Level(string name)=>ClassLevel(c,name);
        var fighter=Level("Fighter");return fighter>=20?4:fighter>=11?3:new[]{"Ranger","Fighter","Paladin","Monk","Barbarian"}.Any(n=>Level(n)>=5)?2:1;
    }
    private static CharacterState Tick(CharacterState c,CombatBoundary boundary,string actor,int amount)
    {
        var expired=new HashSet<string>();var timers=new List<CombatEffectTimer>();
        foreach(var t in c.Combat.Timers.Where(t=>c.Effects.Any(e=>e.Id==t.EffectId)))
        {
            var x=t;
            var counted=t.Duration==CombatDuration.Rounds&&boundary==CombatBoundary.Round||t.Duration==CombatDuration.GameSeconds&&(boundary==CombatBoundary.GameTime||boundary==CombatBoundary.Round);
            if(counted){var delta=t.Duration==CombatDuration.GameSeconds&&boundary==CombatBoundary.Round?amount*6:amount;x=t with{Remaining=Math.Max(0,t.Remaining-delta)};if(x.Remaining==0)expired.Add(x.EffectId);}
            var start=boundary is CombatBoundary.OwnStart or CombatBoundary.ActorStart;var end=boundary is CombatBoundary.OwnEnd or CombatBoundary.ActorEnd;
            if(c.Combat.Sequence>t.CreatedSequence&&((t.Duration==CombatDuration.CreatorNextStart&&start&&actor==t.CreatorId)||(t.Duration==CombatDuration.CreatorNextEnd&&end&&actor==t.CreatorId)||(t.Duration==CombatDuration.TargetNextStart&&start&&actor==t.TargetId)||(t.Duration==CombatDuration.TargetNextEnd&&end&&actor==t.TargetId)))expired.Add(t.EffectId);
            if(t.Duration==CombatDuration.RepeatedSave&&end&&actor==t.TargetId)x=x with{AwaitingSave=true};
            if(!expired.Contains(x.EffectId))timers.Add(x);
        }
        var effects=c.Effects.Where(x=>!expired.Contains(x.Id)).ToList();return c with{Effects=effects,Combat=c.Combat with{Timers=timers},Play=c.Play with{HunterTarget=effects.Any(e=>e.Concentration&&e.Source=="spell:hunters-mark")?c.Play.HunterTarget:""}};
    }
    private static void ValidateLayout(CombatLayout l)
    {
        var sections=new[]{"attacks","spells","skills","saves","effects"};
        if(l.Version!=1||l.Sections.Count!=5||l.Sections.Distinct().Count()!=5||l.Sections.Any(x=>!sections.Contains(x))||l.HiddenSections.Any(x=>!sections.Contains(x)||x=="effects"))throw new ArgumentException("Choose a valid layout; effects remain visible.");
        foreach(var values in new[]{l.PinnedSkills,l.PinnedSpells,l.PinnedFeatures,l.PinnedItems})if(values.Count>100||values.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>160)||values.Distinct().Count()!=values.Count)throw new ArgumentException("Choose unique, bounded layout pins.");
    }
    public static bool HasPendingCast(CharacterState c)=>c.Play.LastCast is {} receipt&&(receipt.Preview.AttackRollCount is>0?receipt.AttacksRemaining is>0:receipt is {EffectTotal:null,Preview.Modeled:true,Preview.Dice:not null} cast&&cast.Preview.Trigger!="attack-hit-marked-target"&&cast.Preview.Resolution!="marked-attack-hit");
    public static void Validate(CharacterState c)
    {
        var s=c.Combat;
        if(s.Version!=1||!Enum.IsDefined(s.Phase)||s.Round<1||s.Round>100000||s.OwnTurnNumber<0||s.AttacksRemaining<0||s.AttacksRemaining>20||s.MovementSpent<0||s.MovementGranted<0||s.Timers.Count>100||s.CompletedBoundaryIds.Count>100)throw new ArgumentException("Invalid combat state.");
        if(s.EncounterActive)WorkspaceValidation.Id(s.EncounterId);
        ValidateLayout(s.Layout);
        foreach(var t in s.Timers)if(!Enum.IsDefined(t.Duration)||t.Remaining<0||t.OriginalAmount<0)throw new ArgumentException("Invalid combat effect timer.");
    }
}
