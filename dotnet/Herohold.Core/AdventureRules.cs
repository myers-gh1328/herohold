namespace Herohold.Core;

/// <summary>Explicit player-owned travel and creature facts, independent of class automation.</summary>
public static class AdventureRules
{
    public static CarryingSummary Carrying(CharacterState c)
    {
        var multiplier=SheetRules.Profile(c).Size?.Trim().ToLowerInvariant() switch{"tiny"=>.5m,"large"=>2m,"huge"=>4m,"gargantuan"=>8m,_=>1m};
        var capacity=c.Sheet.Adventure.CapacityOverrideLb??15m*c.AbilityScores.GetValueOrDefault(Ability.Strength,10)*multiplier;
        var items=c.Equipment.Where(i=>i.Carried&&i.Quantity>0).ToList();
        var weight=items.Sum(i=>(i.UnitWeightLb??0)*i.Quantity);
        if(c.Sheet.Adventure.CarryCoins&&!SheetRules.HasUnresolvedCoins(c)){var coins=SheetRules.Purse(c);weight+=((decimal)coins.Cp+coins.Sp+coins.Ep+coins.Gp+coins.Pp)/50;}
        var unknown=items.Count(i=>i.UnitWeightLb is null);
        return new(weight,capacity,unknown,unknown==0&&(!c.Sheet.Adventure.CarryCoins||!SheetRules.HasUnresolvedCoins(c)));
    }

    public static CharacterState AdjustCharges(CharacterState c,string itemId,int delta)
    {
        var result=DomainCopy.Clone(c);var index=result.Equipment.FindIndex(i=>i.Id==itemId);
        if(index<0)throw new ArgumentException("This item no longer exists.");
        var item=result.Equipment[index];var charges=item.Charges??throw new ArgumentException("This item has no charges.");
        var next=(long)charges.Remaining+delta;
        if(next<0||next>charges.Maximum)throw new ArgumentException("Charges must stay between zero and their maximum.");
        if(delta<0&&(!item.Carried||item.Quantity==0||item.RequiresAttunement&&!item.Attuned))throw new ArgumentException("This item must be available and attuned when required.");
        result.Equipment[index]=item with{Charges=charges with{Remaining=(int)next}};
        WorkspaceRules.Validate(result);return SheetRules.CloseInspirationWindows(result);
    }

    public static IReadOnlyList<MovementMode> Movement(CharacterState c)
    {
        var modes=c.Sheet.Adventure.Movement.Select(m=>m with{}).ToList();
        if(c.Build?.ImportedMovementModes is {} imported)
            foreach(var (key,value) in imported)
                if(value.Number is {} speed&&speed>=0&&speed<=10000&&!modes.Any(m=>m.Kind.Equals(key,StringComparison.OrdinalIgnoreCase)))
                    modes.Add(new(){Kind=key,Speed=speed,Source="Character build"});
        return modes.Select(m=>m with{Speed=Math.Max(0,m.Speed-5*c.Exhaustion)}).ToArray();
    }
    public static CharacterState SetFacts(CharacterState c,AdventureFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var detached=System.Text.Json.JsonSerializer.Deserialize<AdventureFacts>(System.Text.Json.JsonSerializer.Serialize(facts))!;
        var result=DomainCopy.Clone(c);result=result with{Sheet=result.Sheet with{Adventure=detached}};
        WorkspaceRules.Validate(result);return SheetRules.CloseInspirationWindows(result);
    }
    public static CharacterState AdjustCompanionResource(CharacterState c,string companionId,string resourceId,int delta)
    {
        var result=DomainCopy.Clone(c);var companions=result.Sheet.Adventure.Companions;
        var index=companions.FindIndex(p=>p.Id==companionId);if(index<0)throw new ArgumentException("This companion no longer exists.");
        var companion=companions[index];var counter=companion.Resources.FindIndex(r=>r.Id==resourceId);if(counter<0)throw new ArgumentException("This companion counter no longer exists.");
        var resource=companion.Resources[counter];var next=(long)resource.Remaining+delta;if(next<0||next>resource.Maximum)throw new ArgumentException("Companion counters must stay between zero and maximum.");
        companion.Resources[counter]=resource with{Remaining=(int)next};WorkspaceRules.Validate(result);return SheetRules.CloseInspirationWindows(result);
    }
    public static CharacterState AdjustCompanionHp(CharacterState c,string id,int delta)
    {
        var result=DomainCopy.Clone(c);var companions=result.Sheet.Adventure.Companions;
        var index=companions.FindIndex(p=>p.Id==id);if(index<0)throw new ArgumentException("This companion no longer exists.");
        var p=companions[index];var hp=p.CurrentHp;var temp=p.TemporaryHp;
        if(delta<0){var damage=-(long)delta;var absorbed=Math.Min(damage,temp);temp-=(int)absorbed;hp=(int)Math.Max(0,hp-(damage-absorbed));}
        else hp=(int)Math.Min(p.MaximumHp,(long)hp+delta);
        companions[index]=p with{CurrentHp=hp,TemporaryHp=temp};WorkspaceRules.Validate(result);return SheetRules.CloseInspirationWindows(result);
    }

    internal static void Validate(CharacterState c)
    {
        var facts=c.Sheet.Adventure??throw new ArgumentException("Travel facts are missing.");
        if(facts.CapacityOverrideLb is <0 or >100000000||facts.CapacitySource is null||facts.CapacitySource.Length>500||facts.Movement is null||facts.Movement.Count>10||facts.Companions is null||facts.Companions.Count>30)throw new ArgumentException("Travel facts exceed their bounds.");
        foreach(var mode in facts.Movement)
            if(mode is null||mode.Kind is not("Climb" or "Swim" or "Fly" or "Burrow")||mode.Speed is <0 or >10000||mode.Source is null||mode.Source.Length>500||mode.Note is null||mode.Note.Length>2000)throw new ArgumentException("Movement requires a known mode, bounded speed and source note.");
        if(facts.Movement.Select(m=>m.Kind).Distinct().Count()!=facts.Movement.Count)throw new ArgumentException("Record each movement mode once.");
        if(facts.Companions.Any(p=>p is null))throw new ArgumentException("A companion entry is missing.");
        if(facts.Companions.Select(p=>p.Id).Distinct().Count()!=facts.Companions.Count)throw new ArgumentException("Companion identifiers must be unique.");
        foreach(var p in facts.Companions)
        {
            WorkspaceValidation.Id(p.Id);
            if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>200||p.Kind is null||p.Kind.Length>100||p.Source is null||p.Source.Length>500||p.Notes is null||p.Notes.Length>12000||p.ArmorClass is <0 or >100||p.MaximumHp is <1 or >100000||p.CurrentHp<0||p.CurrentHp>p.MaximumHp||p.TemporaryHp is <0 or >100000||p.Speed is <0 or >10000)throw new ArgumentException("Companion values are outside their bounds.");
            if(p.Actions is null||p.Actions.Count>50||p.Actions.Any(a=>a is null||string.IsNullOrWhiteSpace(a.Name)||a.Name.Length>200||a.Rules is null||a.Rules.Length>12000)||p.Resources is null||p.Resources.Count>50||p.Conditions is null||p.Conditions.Count>30||p.Conditions.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>100))throw new ArgumentException("Companion references exceed their bounds.");
            if(p.Resources.Any(r=>r is null))throw new ArgumentException("A companion resource entry is missing.");
            if(p.Resources.Select(r=>r.Id).Distinct().Count()!=p.Resources.Count||p.Resources.Any(r=>string.IsNullOrWhiteSpace(r.Id)||r.Id.Length>64||string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>200||r.Maximum is <0 or >100000||r.Remaining<0||r.Remaining>r.Maximum))throw new ArgumentException("Companion resource counters are invalid.");
        }
        foreach(var item in c.Equipment)
            if(!Enum.IsDefined(item.UseAction)||item.UnitWeightLb is <0 or >1000000||item.Charges is {} charges&&(charges.UseCost<1||charges.UseCost>charges.Maximum||charges.Maximum is <1 or >100000||charges.Remaining<0||charges.Remaining>charges.Maximum||charges.RecoveryNote is null||charges.RecoveryNote.Length>2000))throw new ArgumentException("Item weight or charges are invalid.");
        foreach(var item in c.Equipment)
            if(item.BaseWeapon is null||item.BaseWeapon.Length>200||item.AttackBonus is <-20 or >20||item.DamageBonus is <-20 or >20)throw new ArgumentException("An item's base weapon or weapon bonuses are outside their bounds.");
        foreach(var item in c.Equipment)
            if(item.HealingDice is {} dice&&(dice.Count is <1 or >100||dice.Sides is <2 or >100)||item.HealingBonus is <0 or >1000||item.HealingDice is null&&item.HealingBonus!=0)throw new ArgumentException("An item's healing needs 1–100 dice of 2–100 sides and a bonus from 0 to 1000.");
    }

    public static string? ItemUseReason(CharacterState c,EquipmentItem item)
    {
        if(!item.Carried||item.Quantity<1)return "This item is not available in your pack.";
        if(item.RequiresAttunement&&!item.Attuned)return "Attune to this item before using it.";
        if(item.Charges is {} charges&&charges.Remaining<charges.UseCost)return "This item has insufficient charges.";
        try{PrepareItemUse(c,item,true);return null;}catch(ArgumentException e){return e.Message;}
    }
    internal static CharacterState PrepareItemUse(CharacterState c,EquipmentItem item,bool confirmed)
    {
        if(!c.Combat.EncounterActive)return DomainCopy.Clone(c);
        if(!c.Combat.Enabled)throw new ArgumentException("Resume combat mode or end the encounter before using items.");
        if(!confirmed||item.UseAction==ItemUseAction.TableManaged)throw new ArgumentException("Confirm the item's recorded action cost and its required table facts before using it in combat.");
        if(CombatRules.Incapacitated(c))throw new ArgumentException("Incapacitated: you cannot use this item in combat.");
        var state=c.Combat;
        if(item.UseAction!=ItemUseAction.Reaction&&state.Phase!=CombatPhase.OwnTurn)throw new ArgumentException("Wait for your own turn to use this item.");
        state=item.UseAction switch
        {
            ItemUseAction.Action when state.ActionAvailable=>state with{ActionAvailable=false},
            ItemUseAction.BonusAction when state.BonusActionAvailable=>state with{BonusActionAvailable=false},
            ItemUseAction.Reaction when state.ReactionAvailable=>state with{ReactionAvailable=false},
            ItemUseAction.None=>state,
            _=>throw new ArgumentException("This item's required action has been spent.")
        };
        return DomainCopy.Clone(c) with{Combat=state};
    }
}
