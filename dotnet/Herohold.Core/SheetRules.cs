using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Core;

/// <summary>Owned sheet facts and source-aware references. Reading legacy data never writes a migration.</summary>
public static class SheetRules
{
    public const int MaximumCoins=1_000_000_000;
    public static IReadOnlyList<string> DamageTypes {get;}=Array.AsReadOnly(new[]{"Acid","Bludgeoning","Cold","Fire","Force","Lightning","Necrotic","Piercing","Poison","Psychic","Radiant","Slashing","Thunder"});
    public static IReadOnlyList<string> ConditionTypes {get;}=Array.AsReadOnly(new[]{"Blinded","Charmed","Deafened","Exhaustion","Frightened","Grappled","Incapacitated","Invisible","Paralyzed","Petrified","Poisoned","Prone","Restrained","Stunned","Unconscious"});
    private static string? Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var s)?s:null;
    private static bool TryWhole(ChoiceValue? value,out int number)
    {
        number=0;if(value is null)return false;
        if(value.Number is {} whole){if(whole is <0 or >MaximumCoins)return false;number=whole;return true;}
        if(value.Text is {} text&&decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var parsed)&&parsed>=0&&parsed<=MaximumCoins&&decimal.Truncate(parsed)==parsed){number=(int)parsed;return true;}
        return false;
    }
    private static int Number(ChoiceValue? value,int maximum=MaximumCoins)=>TryWhole(value,out var number)&&number<=maximum?number:0;
    private static bool Present(ChoiceValue? value)=>value is {IsNull:false}&&(value.Text is not {} text||!string.IsNullOrWhiteSpace(text));
    private static ChoiceValue? Pdf(CharacterState c,string key)=>c.Build?.PdfPassthroughFields?.GetValueOrDefault(key);
    private static ChoiceValue? CoinValue(CharacterState c,string key)
    {
        var aliases=new Dictionary<string,string>{{"cp","copper"},{"sp","silver"},{"ep","electrum"},{"gp","gold"},{"pp","platinum"}};
        foreach(var block in new[]{c.Build?.ImportedCoins,c.Build?.ImportedCurrency})
            if(block?.Fields is {} values){var pair=values.FirstOrDefault(p=>p.Key.Equals(key,StringComparison.OrdinalIgnoreCase)||p.Key.Equals(aliases[key],StringComparison.OrdinalIgnoreCase));if(pair.Key is not null)return pair.Value;}
        return Pdf(c,key.ToUpperInvariant());
    }
    private static ChoiceValue? XpValue(CharacterState c)=>c.Build?.ImportedExperiencePoints??c.Build?.ImportedXp??Pdf(c,"XP");
    public static CharacterProfile Profile(CharacterState c)
    {
        ArgumentNullException.ThrowIfNull(c);var p=c.Sheet.Profile;
        string Read(string? owned,string legacy)=>owned??Pdf(c,legacy)?.Text??"";
        return p with{
            Background=p.Background??(c.Build is {Background.Length:>0} build?build.Background:Pdf(c,"Background")?.Text??""),Size=p.Size??c.Derived.Size,
            Appearance=p.Appearance??string.Join(" · ",new[]{("Hair",Read(p.Hair,"Hair")),("Eyes",Read(p.Eyes,"Eyes")),("Skin",Read(p.Skin,"Skin"))}.Where(v=>v.Item2.Length>0).Select(v=>v.Item1+": "+v.Item2)),
            Backstory=Read(p.Backstory,"Backstory"),Personality=Read(p.Personality,"PersonalityTraits "),Alignment=Read(p.Alignment,"Alignment"),
            PlayerName=Read(p.PlayerName,"PlayerName"),Age=Read(p.Age,"Age"),Height=Read(p.Height,"Height"),Weight=Read(p.Weight,"Weight"),Eyes=Read(p.Eyes,"Eyes"),Skin=Read(p.Skin,"Skin"),Hair=Read(p.Hair,"Hair"),
            Ideals=Read(p.Ideals,"Ideals"),Bonds=Read(p.Bonds,"Bonds"),Flaws=Read(p.Flaws,"Flaws"),Allies=Read(p.Allies,"Allies"),Organizations=Read(p.Organizations,"FactionName"),Treasure=Read(p.Treasure,"Treasure"),
            Languages=[..p.Languages??c.Derived.Languages],ArmorTraining=[..p.ArmorTraining??c.Derived.ArmorProficiencies],WeaponProficiencies=[..p.WeaponProficiencies??c.Derived.WeaponProficiencies],ToolProficiencies=[..p.ToolProficiencies??c.Derived.ToolProficiencies]};
    }
    public static CoinPurse Purse(CharacterState c)
    {
        if(c.Sheet.Coins is {} owned)return owned with{};
        int Coin(string key)=>Number(CoinValue(c,key));
        return new(){Cp=Coin("cp"),Sp=Coin("sp"),Ep=Coin("ep"),Gp=Coin("gp"),Pp=Coin("pp")};
    }
    public static IReadOnlyList<string> ImportWarnings(CharacterState c)
    {
        var warnings=new List<string>();if(HasUnresolvedCoins(c))warnings.Add("Imported coin amounts need review. Set confirmed whole-coin balances before receiving or spending coins.");if(HasUnresolvedExperience(c))warnings.Add("Imported XP needs review. Record a confirmed non-negative whole XP value to replace it.");return warnings;
    }
    public static bool HasUnresolvedCoins(CharacterState c)=>c.Sheet.Coins is null&&(new[]{c.Build?.ImportedCoins,c.Build?.ImportedCurrency}.Any(block=>block is {IsNull:false,Fields:null})||new[]{"cp","sp","ep","gp","pp"}.Any(key=>Present(CoinValue(c,key))&&!TryWhole(CoinValue(c,key),out _)));
    public static bool HasUnresolvedExperience(CharacterState c)=>c.Sheet.ExperiencePoints is null&&Present(XpValue(c))&&!TryWhole(XpValue(c),out _);
    public static int ExperiencePoints(CharacterState c)=>c.Sheet.ExperiencePoints??Number(XpValue(c));
    public static bool HasHeroicInspiration(CharacterState c)
    {
        if(c.Sheet.HeroicInspiration is {} owned)return owned;var value=c.Build?.ImportedHeroicInspiration??Pdf(c,"Inspiration");
        return value?.Flag??value?.Text?.Trim().ToLowerInvariant() is "true" or "yes" or "on" or "1";
    }
    public static CharacterSheetState ImportState(JsonObject raw,int currentHp)
    {
        var pdf=raw["pdf_passthrough_fields"];
        bool Checked(string name)=>pdf?[name] is JsonValue value&&(value.TryGetValue<bool>(out var check)&&check||Text(value)?.ToLowerInvariant() is "yes" or "true" or "on");
        var successes=currentHp==0?new[]{12,13,14}.Count(n=>Checked("Check Box "+n)):0;
        var failures=currentHp==0?new[]{15,16,17}.Count(n=>Checked("Check Box "+n)):0;
        // Free-text profile facts have no build or rules home; an imported character keeps them as its own profile.
        string? Own(string key)=>Text(raw[key]);
        var profile=new CharacterProfile{Appearance=Own("appearance"),Backstory=Own("backstory"),Personality=Own("personality"),Alignment=Own("alignment"),PlayerName=Own("player_name"),Age=Own("age"),Height=Own("height"),Weight=Own("weight"),
            Eyes=Own("eyes"),Skin=Own("skin"),Hair=Own("hair"),Ideals=Own("ideals"),Bonds=Own("bonds"),Flaws=Own("flaws"),Allies=Own("allies"),Organizations=Own("organizations"),Treasure=Own("treasure")};
        return new(){Profile=profile,DeathSaves=failures>=3?new(){Failures=3,Dead=true}:successes>=3?new(){Stable=true}:new(){Successes=successes,Failures=failures}};
    }
    public static CharacterState CloseInspirationWindows(CharacterState c)=>c with{Play=c.Play with{LastRoll=c.Play.LastRoll is{} roll?roll with{InspirationWindowId=null}:null,LastCast=c.Play.LastCast is{} cast?cast with{InspirationWindowId=null}:null}};
    internal static CharacterSheetState Clone(CharacterSheetState state)=>JsonSerializer.Deserialize<CharacterSheetState>(JsonSerializer.Serialize(state))!;
    internal static EquipmentItem CloneItem(EquipmentItem item)=>item with{ModifierBonuses=new(item.ModifierBonuses),Defenses=item.Defenses.Select(d=>d with{}).ToList(),PassiveEffects=item.PassiveEffects.Select(e=>e with{}).ToList(),UseEffects=item.UseEffects.Select(e=>e with{}).ToList()};
    public static CharacterState UpdateProfile(CharacterState c,CharacterProfile profile,int? experiencePoints)
    {
        ArgumentNullException.ThrowIfNull(profile);var result=DomainCopy.Clone(c) with{Sheet=c.Sheet with{Profile=Clone(c.Sheet with{Profile=profile}).Profile,ExperiencePoints=experiencePoints??c.Sheet.ExperiencePoints}};WorkspaceValidation.Character(result);return CloseInspirationWindows(result);
    }
    public static CharacterState ChangeCoins(CharacterState c,CoinChangeKind kind,CoinPurse amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);ValidatePurse(amounts);if(kind!=CoinChangeKind.Set&&HasUnresolvedCoins(c))throw new ArgumentException("Review the imported amounts and explicitly set confirmed coin balances first.");if(!Enum.IsDefined(kind))throw new ArgumentException("Choose receive, spend or set balances.");var old=Purse(c);
        int Change(int current,int value){var next=kind==CoinChangeKind.Set?value:(long)current+(kind==CoinChangeKind.Spend?-value:value);if(next<0||next>MaximumCoins)throw new ArgumentException("The compound coin change exceeds an available balance or the coin limit. No coins were changed.");return(int)next;}
        var next=new CoinPurse{Cp=Change(old.Cp,amounts.Cp),Sp=Change(old.Sp,amounts.Sp),Ep=Change(old.Ep,amounts.Ep),Gp=Change(old.Gp,amounts.Gp),Pp=Change(old.Pp,amounts.Pp)};
        var result=DomainCopy.Clone(c);return CloseInspirationWindows(result with{Sheet=result.Sheet with{Coins=next}});
    }
    public static CharacterState SetHeroicInspiration(CharacterState c,bool held){var result=DomainCopy.Clone(c);return CloseInspirationWindows(result with{Sheet=result.Sheet with{HeroicInspiration=held}});}
    public static CharacterState SetDeathSaves(CharacterState c,DeathSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);if(state.Successes is<0 or>3||state.Failures is<0 or>3||state.Stable&&state.Dead)throw new ArgumentException("Death-save counts must be0–3 and stable/dead states cannot overlap.");
        if(state.Failures==3)state=state with{Dead=true,Stable=false};else if(state.Successes==3||state.Stable)state=new(){Stable=true};
        if(c.CurrentHp>0&&(state.Successes>0||state.Failures>0||state.Stable||state.Dead))throw new ArgumentException("Death saves require zero HP. Clear them when the character regains HP.");
        var result=DomainCopy.Clone(c);result=result with{Sheet=result.Sheet with{DeathSaves=state with{}}};WorkspaceValidation.Character(result);return CloseInspirationWindows(result);
    }
    public static CharacterState RecordDeathSave(CharacterState c,int d20)
    {
        c=CombatRules.PrepareDeathSave(c);
        if(d20 is<1 or>20)throw new ArgumentException("Enter an unmodified d20 from1–20.");if(c.CurrentHp!=0||c.Sheet.DeathSaves.Stable||c.Sheet.DeathSaves.Dead)throw new ArgumentException("Only a living, unstable character at zero HP makes death saves.");
        if(d20==20)return CombatRules.FinalizeDeathSave(WorkspaceRules.AdjustHp(c,1));
        var old=c.Sheet.DeathSaves;return CombatRules.FinalizeDeathSave(SetDeathSaves(c,d20-2*c.Exhaustion>=10?old with{Successes=Math.Min(3,old.Successes+1)}:old with{Failures=Math.Min(3,old.Failures+(d20==1?2:1))}));
    }
    public static CharacterState Stabilize(CharacterState c)
    {
        if(c.CurrentHp!=0||c.Sheet.DeathSaves.Dead)throw new ArgumentException("Only a living character at zero HP can be stabilized.");return SetDeathSaves(c,new(){Stable=true});
    }
    private static IEnumerable<EquipmentItem> ActiveItems(CharacterState c)=>c.Equipment.Where(i=>i.Carried&&i.Quantity>0&&i.Equipped&&(!i.RequiresAttunement||i.Attuned));
    public static IReadOnlyList<DamageDefense> EffectiveDefenses(CharacterState c)=>c.Defenses.Select(d=>d with{}).Concat(ActiveItems(c).SelectMany(i=>i.Defenses.Select(d=>d with{Source="item:"+i.Id+" · "+i.Name+(string.IsNullOrEmpty(d.Source)?"":" · "+d.Source)}))).ToArray();
    public static IReadOnlyList<ActiveEffect> EffectiveEffects(CharacterState c)
    {
        var effects=c.Effects.Select(e=>e with{}).ToList();
        var used=new HashSet<string>(effects.Select(e=>e.Id),StringComparer.Ordinal);
        string DerivedId(string kind,string source,int index)
        {
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
            var basis=$"derived-{kind}-{hash}-{index}";var id=basis;var suffix=0;
            while(!used.Add(id))id=basis+"-"+(++suffix).ToString(CultureInfo.InvariantCulture);
            return id;
        }
        foreach(var item in ActiveItems(c))
            for(var index=0;index<item.PassiveEffects.Count;index++)
                effects.Add(item.PassiveEffects[index] with{Id=DerivedId("item",item.Id,index),Source="item:"+item.Id+" · "+item.Name});
        if(c.CurrentHp==0&&!c.Sheet.DeathSaves.Dead)
            effects.Add(new(){Id=DerivedId("health","zero-hp",0),Name="Unconscious",Source="Zero hit points",Note=c.Sheet.DeathSaves.Stable?"Stable; remains unconscious until HP returns":"Make death saves at the start of your turn"});
        return effects;
    }
    internal static CharacterState ApplyEffectiveIncapacitation(CharacterState c)
    {
        if(!EffectiveEffects(c).Any(e=>WorkspaceRules.IsIncapacitatingCondition(e.Name)))return c;
        return c with{Effects=c.Effects.Where(e=>!e.Concentration).ToList(),Play=c.Play with{HunterTarget=string.Empty}};
    }
    public static CharacterState UseEquipment(CharacterState c,string itemId,bool confirmTableFacts=false)
    {
        var item=c.Equipment.FirstOrDefault(i=>i.Id==itemId)??throw new ArgumentException("This item no longer exists.");if(!item.Carried||item.Quantity<1||!item.Consumable&&item.Charges is null&&item.UseEffects.Count==0)throw new ArgumentException("This item has no available use.");if(item.RequiresAttunement&&!item.Attuned)throw new ArgumentException("Attune to this item before using it.");
        var result=AdventureRules.PrepareItemUse(c,item,confirmTableFacts);
        if(item.Charges is {} charges)result=AdventureRules.AdjustCharges(result,itemId,-charges.UseCost);
        if(item.Consumable)result=WorkspaceRules.AdjustQuantity(result,itemId,-1);
        foreach(var effect in item.UseEffects)result=WorkspaceRules.AddEffect(result,effect with{Id=Guid.NewGuid().ToString("N"),Source="item:"+item.Id+" · "+item.Name});WorkspaceValidation.Character(result);return CloseInspirationWindows(result);
    }
    internal static void Validate(CharacterState c)
    {
        if(c.Sheet is null||c.Sheet.Profile is null||c.Sheet.DeathSaves is null)throw new ArgumentException("Sheet state is missing.");var sheet=c.Sheet;var p=sheet.Profile;
        if(sheet.ExperiencePoints is<0 or>MaximumCoins)throw new ArgumentException("XP must be a non-negative bounded integer.");if(sheet.Coins is{} coins)ValidatePurse(coins);
        foreach(var value in new[]{p.Background,p.Size,p.Alignment,p.PlayerName,p.Age,p.Height,p.Weight,p.Eyes,p.Skin,p.Hair})if(value?.Length>200)throw new ArgumentException("A profile reference exceeds200characters.");
        foreach(var value in new[]{p.Appearance,p.Backstory,p.Personality,p.Ideals,p.Bonds,p.Flaws,p.Allies,p.Organizations,p.Treasure})if(value?.Length>20000)throw new ArgumentException("A profile note exceeds20000characters.");
        foreach(var list in new[]{p.Languages,p.ArmorTraining,p.WeaponProficiencies,p.ToolProficiencies})if(list is not null&&(list.Count>100||list.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>200)||list.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=list.Count))throw new ArgumentException("A training or language list is invalid.");
        var d=sheet.DeathSaves;if(d.Successes is<0 or>3||d.Failures is<0 or>3||d.Stable&&d.Dead||d.Stable&&(d.Successes!=0||d.Failures!=0)||c.CurrentHp>0&&(d.Successes!=0||d.Failures!=0||d.Stable||d.Dead))throw new ArgumentException("Death saves conflict with the character's HP or survival state.");
        if(sheet.AmmunitionFired is null||sheet.AmmunitionFired.Count>20||sheet.AmmunitionFired.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Key.Length>200||p.Value is<1 or>100000))throw new ArgumentException("Fired ammunition counts are invalid.");
        foreach(var item in c.Equipment)
        {
            if(item.Defenses is null||item.PassiveEffects is null||item.UseEffects is null||item.Defenses.Count>30||item.PassiveEffects.Count>20||item.UseEffects.Count>20)throw new ArgumentException("Item effect lists are invalid.");
            foreach(var defense in item.Defenses)if(defense is null||defense.Kind is not("resistance" or "immunity" or "vulnerability" or "conditionImmunity")||string.IsNullOrWhiteSpace(defense.Type)||defense.Type.Length>100||defense.Source is null||defense.Source.Length>500||defense.Note is null||defense.Note.Length>2000)throw new ArgumentException("Item defenses require a supported kind and bounded non-empty reference type.");
            foreach(var e in item.PassiveEffects.Concat(item.UseEffects))if(e is null||string.IsNullOrWhiteSpace(e.Name)||e.Name.Length>200||e.Source is null||e.Source.Length>500||e.Note is null||e.Note.Length>2000||e.RemainingRounds is<=0 or>10000)throw new ArgumentException("An item effect is invalid.");
        }
    }
    private static void ValidatePurse(CoinPurse p){if(new[]{p.Cp,p.Sp,p.Ep,p.Gp,p.Pp}.Any(v=>v<0||v>MaximumCoins))throw new ArgumentException("Coin balances must be whole non-negative values within the supported limit.");}
}
