namespace Herohold.Core;
public static partial class PlayRules
{
    public static CharacterState UseHeroicInspiration(WorkspaceSnapshot workspace,HeroicInspirationRequest request,Func<int,int>? dice=null)
    {
        if(AlreadyCompleted(workspace,request))return DomainCopy.Clone(workspace.Character);
        var c=workspace.Character;if(!SheetRules.HasHeroicInspiration(c))throw new ArgumentException("Heroic Inspiration is not held.");
        if(!Enum.IsDefined(request.Kind))throw new ArgumentException("Choose the die you just rolled.");
        void Window(string? window){if(window is null||c.Play.CompletedOperationIds.LastOrDefault()!=window)throw new ArgumentException("Heroic Inspiration must reroll a die immediately after that roll, before another action.");}
        int Replacement(int sides,DiceMode mode)=>PlayDice.Roll(new(1,sides),mode,(request.PhysicalDie??request.PhysicalD20) is{} value?[value]:[],dice)[0];
        var after=SheetRules.SetHeroicInspiration(c,false);
        if(request.Kind==HeroicDieKind.SpellEffect)
        {
            var cast=c.Play.LastCast;if(cast is null||cast.Id!=request.CastId||cast.EffectTotal is null||cast.Preview.Dice is null||request.DieIndex<0||request.DieIndex>=cast.EffectDice.Count)throw new ArgumentException("Choose one die from the spell effect you just rolled.");
            Window(cast.InspirationWindowId);var values=cast.EffectDice.ToList();values[request.DieIndex]=Replacement(cast.Preview.Dice.Sides,cast.Mode);
            after=after with{Play=after.Play with{LastCast=cast with{EffectDice=values,EffectTotal=Math.Max(0,values.Sum()+cast.Preview.Modifier)}}};
        }
        else
        {
            var roll=c.Play.LastRoll;if(roll is null||roll.Id!=request.RollId)throw new ArgumentException("Choose the current roll.");Window(roll.InspirationWindowId);
            if(request.Kind==HeroicDieKind.BonusDie)
            {
                if(roll.Resolved||roll.BonusDieSides is not{} sides||request.DieIndex<0||request.DieIndex>=roll.BonusDice.Count)throw new ArgumentException("Choose a bonus die participating in this unresolved roll.");
                var values=roll.BonusDice.ToList();values[request.DieIndex]=Replacement(sides,roll.Mode);after=after with{Play=after.Play with{LastRoll=roll with{BonusDice=values,Total=roll.ChosenD20+roll.StandingBonus.Total+roll.FlatBonus+values.Sum()}}};
            }
            else if(request.Kind==HeroicDieKind.Damage)
            {
                if(!roll.Resolved||roll.Hit is not true||request.DamageTermIndex<0||request.DamageTermIndex>=roll.Damage.Count)throw new ArgumentException("Choose damage from the attack you just resolved.");
                var term=roll.Damage[request.DamageTermIndex];if(term.DieSides is not{} sides||request.DieIndex<0||request.DieIndex>=term.Dice.Count)throw new ArgumentException("This damage die has no recorded size or is no longer eligible.");
                var values=term.Dice.ToList();values[request.DieIndex]=Replacement(sides,roll.Mode);var damage=roll.Damage.Select(t=>t with{Dice=[..t.Dice]}).ToList();damage[request.DamageTermIndex]=term with{Dice=values,Total=Math.Max(0,values.Sum()+term.Modifier)};
                after=after with{Play=after.Play with{LastRoll=roll with{Damage=damage}}};
            }
            else
            {
                if(roll.Resolved||request.DieIndex<0||request.DieIndex>=roll.D20.Count||roll.Advantage==RollAdvantage.Normal&&request.DieIndex!=0)throw new ArgumentException("Choose one participating d20 before resolving this roll.");
                var values=roll.D20.ToList();values[request.DieIndex]=Replacement(20,roll.Mode);var chosen=roll.Advantage==RollAdvantage.Advantage?values.Max():roll.Advantage==RollAdvantage.Disadvantage?values.Min():values[0];
                after=after with{Play=after.Play with{LastRoll=roll with{D20=values,ChosenD20=chosen,Total=chosen+roll.StandingBonus.Total+roll.FlatBonus+roll.BonusDice.Sum()}}};
            }
        }
        return Commit(c,after,request,"Spend Heroic Inspiration to reroll one die; use the replacement");
    }
}
