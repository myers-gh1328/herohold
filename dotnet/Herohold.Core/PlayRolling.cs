namespace Herohold.Core;

public static partial class PlayRules
{
    public static CharacterState Roll(WorkspaceSnapshot w, RollRequest r, Func<int, int>? dice = null)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        if (!Enum.IsDefined(r.AttackKind)||!Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.Advantage) || !Enum.IsDefined(r.Ability)) throw new ArgumentException("Choose a valid roll kind, advantage, and ability.");
        if (r.Name is null || r.Name.Length > 200 || r.FlatBonus is < -100 or > 100 || r.StandingFlatBonus is < -100 or > 100)
            throw new ArgumentException("Roll labels or adjustments are outside their limits.");
        var c = CombatRules.PrepareRoll(w.Character,r);
        var standing = r.Kind switch
        {
            RollKind.Attack => CharacterCalculations.Attack(c, r.Ability, r.Proficient, r.StandingFlatBonus),
            RollKind.Save => CharacterCalculations.Save(c, r.Ability),
            RollKind.Skill => CharacterCalculations.Skill(c, r.Skill, r.Ability),
            RollKind.Initiative => CharacterCalculations.Initiative(c),
            RollKind.Ability => CharacterCalculations.AbilityCheck(c,r.Ability),
            RollKind.SpellAttack => CharacterCalculations.SpellAttack(c,r.Ability),
            _ => throw new ArgumentException("Unsupported roll kind.")
        };
        var d20 = PlayDice.Roll(new(r.Advantage == RollAdvantage.Normal ? 1 : 2, 20), r.Mode, r.PhysicalD20, dice);
        var chosen = r.Advantage == RollAdvantage.Advantage ? d20.Max() : r.Advantage == RollAdvantage.Disadvantage ? d20.Min() : d20[0];
        var bonus = r.BonusDice is null ? new List<int>() : PlayDice.Roll(r.BonusDice, r.Mode, r.PhysicalBonusDice, dice);
        if (r.BonusDice is null && r.PhysicalBonusDice.Count > 0) throw new ArgumentException("Declare bonus dice before entering their results.");
        var after = DomainCopy.Clone(c);
        after = after with { Play = after.Play with { LastRoll = new()
        {
            AttackKind=r.Kind==RollKind.SpellAttack?AttackKind.Spell:r.Kind==RollKind.Attack?r.AttackKind:AttackKind.Unknown,InspirationWindowId=r.OperationId,Id = r.OperationId, Name = r.Name.Trim(), Kind = r.Kind, Mode = r.Mode, Advantage = r.Advantage,
            D20 = d20, BonusDice = bonus, BonusDieSides=r.BonusDice?.Sides,StandingBonus = standing, FlatBonus = r.FlatBonus, ChosenD20 = chosen,
            Total = chosen + standing.Total + r.FlatBonus + bonus.Sum()
        } } };
        after=CombatRules.FinalizeRoll(after,r);
        return RecordCompleted(w.Character, after, r);
    }

    public static CharacterState UseLucky(WorkspaceSnapshot w, LuckyRequest r, Func<int, int>? dice = null)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        var roll = w.Character.Play.LastRoll;
        if (roll is null || roll.Id != r.RollId || roll.Resolved || roll.LuckyUsed) throw new ArgumentException("Lucky requires this unresolved roll and can be used only once.");
        if (roll.Advantage == RollAdvantage.Advantage) throw new ArgumentException("This roll already has Advantage; Lucky cannot add a third d20.");
        var after = WorkspaceRules.AdjustResource(w.Character, "luck", -1);
        List<int> d20;
        int chosen;
        RollAdvantage advantage;
        if (roll.Advantage == RollAdvantage.Disadvantage)
        {
            // Advantage and Disadvantage cancel. Preserve the first pre-existing die, never choose the better one retroactively.
            d20 = [.. roll.D20]; chosen = d20[0]; advantage = RollAdvantage.Normal;
        }
        else
        {
            var extra = PlayDice.Roll(new(1, 20), roll.Mode, r.PhysicalD20 is { } physical ? [physical] : [], dice)[0];
            d20 = [.. roll.D20, extra]; chosen = d20.Max(); advantage = RollAdvantage.Advantage;
        }
        after = after with { Play = after.Play with { LastRoll = roll with
        {
            D20 = d20, ChosenD20 = chosen, Advantage = advantage, LuckyUsed = true,InspirationWindowId=r.OperationId,
            Total = chosen + roll.StandingBonus.Total + roll.FlatBonus + roll.BonusDice.Sum()
        } } };
        return Commit(w.Character, after, r, "Spend Luck for " + roll.Name);
    }

    public static CharacterState ResolveRoll(WorkspaceSnapshot w, ResolveRollRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        var roll = w.Character.Play.LastRoll;
        if (roll is null || roll.Id != r.RollId || roll.Resolved) throw new ArgumentException("That roll is no longer awaiting confirmation.");
        if (roll.Kind == RollKind.Attack||roll.Kind==RollKind.SpellAttack&&w.Character.Combat.AttackSelection is {AttackProfile:not null} selection&&selection.RollId==roll.Id) throw new ArgumentException("Declare a hit or miss to resolve an attack.");
        var after = DomainCopy.Clone(w.Character);
        return RecordCompleted(w.Character, after with { Play = after.Play with { LastRoll = roll with { Resolved = true } } }, r);
    }

    public static CharacterState ResolveAttack(WorkspaceSnapshot w, AttackResolutionRequest r, Func<int, int>? dice = null)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        CombatRules.ValidateHit(w.Character,r);
        var roll = w.Character.Play.LastRoll;
        if (roll is null || roll.Id != r.RollId || roll.Kind is not(RollKind.Attack or RollKind.SpellAttack) || roll.Resolved) throw new ArgumentException("That attack is no longer awaiting hit or miss confirmation.");
        if (roll.ChosenD20 == 1 && r.Hit) throw new ArgumentException("A natural 1 on an attack roll misses. Confirm a miss for this roll.");
        if (roll.ChosenD20 == 20 && !r.Hit) throw new ArgumentException("A natural 20 on an attack roll hits critically. Confirm a hit for this roll.");
        var after = DomainCopy.Clone(w.Character);
        var terms = new List<DamageTerm>();
        if (r.Hit)
        {
            if(r.BaseFixedDamage is <0 or >100)throw new ArgumentException("Fixed damage is outside its limits.");
            if(r.BaseFixedDamage is null)PlayDice.ValidateFormula(r.BaseDamage);
            if (r.DamageModifier is < -100 or > 100 || string.IsNullOrWhiteSpace(r.DamageType) || r.DamageType.Length > 100)
                throw new ArgumentException("Declare a valid damage modifier and damage type.");
            if (r.HuntersMark && (!after.Effects.Any(x => x.Concentration && x.Source == "spell:hunters-mark") || string.IsNullOrWhiteSpace(after.Play.HunterTarget)))
                throw new ArgumentException("Hunter’s Mark requires your active marked target and concentration.");
            if (r.HuntersMark && !string.IsNullOrWhiteSpace(r.Target) && !r.Target.Equals(after.Play.HunterTarget, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("This target is not the currently marked quarry.");
            if(r.DreadfulStrike&&roll.AttackKind!=AttackKind.Weapon)throw new ArgumentException("Dreadful Strike requires a recorded weapon attack. Unarmed, spell and unconfirmed legacy attack sources do not qualify.");
            if (r.DreadfulStrike && (after.Play.DreadUsedThisTurn || !RangerRules.IsGloomStalker(after)))
                throw new ArgumentException("Dreadful Strike is unavailable or already used this turn.");
            var multiplier = r.Critical || roll.ChosenD20 == 20 ? 2 : 1;
            var required = (r.BaseFixedDamage is null?r.BaseDamage.Count * multiplier:0) + (r.HuntersMark ? multiplier : 0) + (r.DreadfulStrike ? 2 * multiplier : 0);
            if (roll.Mode == DiceMode.Physical && r.PhysicalDamageDice.Count != required)
                throw new ArgumentException($"Enter exactly {required} damage dice for the selected effects.");
            var physical = new Queue<int>(r.PhysicalDamageDice);
            DamageTerm Term(string name, string type, DiceFormula formula, int modifier)
            {
                var values = PlayDice.Roll(formula, roll.Mode, roll.Mode == DiceMode.Physical ? Enumerable.Range(0, formula.Count).Select(_ => physical.Dequeue()).ToList() : [], dice);
                return new() { Name = name, DamageType = type, DieSides=formula.Sides,Dice = values, Modifier = modifier, Total = Math.Max(0, values.Sum() + modifier) };
            }
            if(r.BaseFixedDamage is { } fixedDamage)terms.Add(new(){Name="Unarmed",DamageType=r.DamageType,Dice=[],Modifier=fixedDamage+r.DamageModifier,Total=Math.Max(0,fixedDamage+r.DamageModifier)});
            else terms.Add(Term(roll.Kind==RollKind.SpellAttack?"Spell":"Weapon", r.DamageType, r.BaseDamage with { Count = r.BaseDamage.Count * multiplier }, r.DamageModifier));
            if (r.HuntersMark) terms.Add(Term("Hunter’s Mark", "Force", RangerRules.HuntersMarkDamage(after) with { Count = multiplier }, 0));
            if (r.DreadfulStrike)
            {
                terms.Add(Term("Dreadful Strike", "Psychic", RangerRules.DreadfulStrikeDamage(after) with { Count = 2 * multiplier }, 0));
                after = WorkspaceRules.AdjustResource(after, "dread", -1);
                after = after with { Play = after.Play with { DreadUsedThisTurn = true } };
            }
        }
        after = after with { Play = after.Play with { LastRoll = roll with { Resolved = true, Hit = r.Hit, Damage = terms,InspirationWindowId=r.OperationId } } };
        return r.Hit && r.DreadfulStrike ? Commit(w.Character, after, r, "Dreadful Strike on confirmed hit") : RecordCompleted(w.Character, after, r);
    }
}
