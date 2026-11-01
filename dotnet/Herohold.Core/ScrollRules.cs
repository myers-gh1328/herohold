namespace Herohold.Core;

/// <summary>The spell on a Spell Scroll as the rules list it, and the classes whose spell list it is on.</summary>
public sealed record ScrollSpell(SpellDefinition Spell,IReadOnlyList<string> Classes);

/// <summary>Finds the spell a scroll names. For stores and pages used without the rules data there is none.</summary>
public interface IScrollSpells
{
    ScrollSpell? Find(string name);
}

/// <summary>
/// Reading a Spell Scroll (2024): the spell must be on your spell list, takes its normal casting time, and the scroll crumbles
/// once it is cast. A spell above the level you can cast needs a spellcasting ability check (DC 10 + its level); on a failure
/// the spell fades and the scroll is lost. The scroll's own save DC and attack bonus apply to the spell.
/// </summary>
public static class ScrollRules
{
    /// <summary>The scroll's spell save DC and attack bonus for a spell of this level.</summary>
    public static (int SaveDc,int AttackBonus) Numbers(int level)=>level switch{<=2=>(13,5),<=4=>(15,7),<=6=>(17,9),<=8=>(18,10),_=>(19,11)};
    public static int CheckDc(int level)=>10+level;
    /// <summary>The highest spell slot level the character has.</summary>
    public static int HighestSlotLevel(CharacterState c)=>c.Resources.Where(r=>r.SlotLevel is not null&&r.Maximum>0).Select(r=>r.SlotLevel!.Value).DefaultIfEmpty(0).Max();
    public static bool OnYourList(CharacterState c,ScrollSpell scroll)
    {
        var classes=c.Build?.ClassLevels?.Keys.ToList() is {Count:>0} levels?levels:[c.ClassName];
        return scroll.Classes.Intersect(classes,StringComparer.OrdinalIgnoreCase).Any()||c.Play.Spells.Any(s=>s.Name.Equals(scroll.Spell.Name,StringComparison.OrdinalIgnoreCase));
    }
    public static bool NeedsCheck(CharacterState c,ScrollSpell scroll)=>scroll.Spell.Level>0&&scroll.Spell.Level>HighestSlotLevel(c);
    public static Calculation Check(CharacterState c)=>CharacterCalculations.AbilityCheck(c,c.Play.SpellcastingAbility);
    public static bool CheckSucceeds(CharacterState c,ScrollSpell scroll,int d20)=>d20+Check(c).Total>=CheckDc(scroll.Spell.Level);

    /// <summary>Why the scroll cannot be read now, or null.</summary>
    public static string? Reason(CharacterState c,EquipmentItem item,ScrollSpell? scroll)
    {
        try{Prepare(c,item,scroll,true);return null;}catch(ArgumentException e){return e.Message;}
    }

    /// <summary>Reads the scroll: it is spent whether the spell is cast or fades on a failed check.</summary>
    /// <param name="checkD20">The d20 of the spellcasting ability check, when the spell is above the level you can cast.</param>
    public static CharacterState Read(CharacterState c,string itemId,ScrollSpell scroll,int? checkD20,bool confirmTableFacts)
    {
        var item=c.Equipment.FirstOrDefault(i=>i.Id==itemId)??throw new ArgumentException("This item no longer exists.");
        if(!item.ScrollSpell.Equals(scroll.Spell.Name,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("This scroll holds another spell.");
        var needsCheck=NeedsCheck(c,scroll);
        if(needsCheck&&checkD20 is not (>=1 and <=20))throw new ArgumentException($"Enter the d20 of your DC {CheckDc(scroll.Spell.Level)} spellcasting ability check.");
        if(!needsCheck&&checkD20 is not null)throw new ArgumentException("This spell needs no check.");
        var result=WorkspaceRules.AdjustQuantity(Prepare(c,item,scroll,confirmTableFacts),item.Id,-1);
        if(scroll.Spell.Concentration&&(!needsCheck||CheckSucceeds(c,scroll,checkD20!.Value)))
        {
            var (dc,attack)=Numbers(scroll.Spell.Level);
            result=WorkspaceRules.AddEffect(result,new(){Id=Guid.NewGuid().ToString("N"),Name=scroll.Spell.Name,Source="item:"+item.Id+" · "+item.Name,Concentration=true,Note=$"Cast from a Spell Scroll · save DC {dc}, attack +{attack}"});
        }
        WorkspaceValidation.Character(result);return SheetRules.CloseInspirationWindows(result);
    }

    private static CharacterState Prepare(CharacterState c,EquipmentItem item,ScrollSpell? scroll,bool confirmed)
    {
        if(item.ScrollSpell.Length==0)throw new ArgumentException("No spell is written on this scroll.");
        if(scroll is null)throw new ArgumentException($"{item.ScrollSpell} is not a spell in the rules data.");
        if(!item.Carried||item.Quantity<1)throw new ArgumentException("This scroll is not in your pack.");
        if(!OnYourList(c,scroll))throw new ArgumentException($"{scroll.Spell.Name} is not on your spell list, so the scroll is unintelligible to you.");
        if(!c.Combat.EncounterActive)return DomainCopy.Clone(c);
        var time=scroll.Spell.CastingTime.Trim();
        var cost=time.Contains("Bonus",StringComparison.OrdinalIgnoreCase)?ItemUseAction.BonusAction:time.Contains("Reaction",StringComparison.OrdinalIgnoreCase)?ItemUseAction.Reaction
            :time.Equals("Action",StringComparison.OrdinalIgnoreCase)||time.Equals("1 action",StringComparison.OrdinalIgnoreCase)?ItemUseAction.Action
            :throw new ArgumentException($"Casting {scroll.Spell.Name} takes {time}, longer than a turn. Read it outside combat.");
        return AdventureRules.PrepareItemUse(c,item with{UseAction=cost},confirmed);
    }
}
