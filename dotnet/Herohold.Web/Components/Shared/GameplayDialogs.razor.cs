using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Security.Cryptography;

namespace Herohold.Web.Components.Shared;

public partial class GameplayDialogs
{
    [Parameter, EditorRequired] public CharacterState Character { get; set; } = default!;
    [Parameter, EditorRequired] public WorkspaceSession Session { get; set; } = default!;
    [Parameter] public DiceMode PreferredDiceMode {get;set;}=DiceMode.Digital;
    [Parameter] public bool QuickRoll {get;set;}
    [Parameter] public EventCallback OnRollOptionsChanged {get;set;}
    private RollAdvantage PresetAdvantage,OptionsAdvantage;
    private int PresetFlat, PresetCount, PresetSides=4,OptionsFlat,OptionsCount,OptionsSides=4;
    public string NextRollSummary=>PresetAdvantage==RollAdvantage.Normal&&PresetFlat==0&&PresetCount==0?"Roll options":"Next: "+string.Join(" · ",new[]{PresetAdvantage==RollAdvantage.Normal?null:PresetAdvantage.ToString(),PresetFlat==0?null:Signed(PresetFlat),PresetCount==0?null:$"{PresetCount}d{PresetSides}"}.Where(s=>s is not null));
    public async Task OpenRollOptionsAsync()
    {
        OptionsAdvantage=PresetAdvantage;OptionsFlat=PresetFlat;OptionsCount=PresetCount;OptionsSides=PresetSides;
        await OpenAsync("next-roll-options-dialog");
    }
    private async Task SaveRollOptionsAsync()
    {
        if(!Enum.IsDefined(OptionsAdvantage)||OptionsFlat is < -100 or >100||OptionsCount is <0 or >10||!new[]{4,6,8,10,12,20}.Contains(OptionsSides)){LocalError="Choose a valid advantage, adjustment and bonus-dice preset.";return;}
        PresetAdvantage=OptionsAdvantage;PresetFlat=OptionsFlat;PresetCount=OptionsCount;PresetSides=OptionsSides;
        await OnRollOptionsChanged.InvokeAsync();await CloseAsync("next-roll-options-dialog");
    }
    [Parameter] public EventCallback<int> OnHealingRequested { get; set; }
    private async Task RequestHealing(int amount){await CloseAsync("spell-cast-dialog");await OnHealingRequested.InvokeAsync(amount);}
    [Inject] private IJSRuntime JS { get; set; } = default!;
    private CharacterState Current => Session.Character ?? Character;
    private string? LocalError;
    private bool Working;
    private DiceMode Mode = DiceMode.Digital;
    private RollRequest RollDraft = new();
    private Calculation Standing = new(0, []);
    private string? ActiveRollId;
    private RollOutcome? Roll => Current.Play.LastRoll is { } r && r.Id == ActiveRollId ? r : null;
    private int FlatBonus;
    private int BonusCount;
    private int BonusSides = 4;
    private RollAdvantage Advantage;
    private string PhysicalD20 = "";
    private string PhysicalBonus = "";
    private int? LuckyD20;
    private bool MarkDamage;
    private bool DreadDamage;
    private string DamageDice = "";
    private string DamageTarget = "";
    private bool CriticalHit;
    private int? WeaponModifier;
    private int? WeaponFixedDamage;
    private DiceFormula WeaponDamage = new(1, 8);
    private string WeaponDamageType = "Piercing";
    private SpellDefinition? CastSpell;
    private CastRequest CastDraft = new();
    private string CastPayment = "";
    private string CastTarget = "";
    private bool CastQualifyingHit;
    private bool CastConcentration;
    private bool CastReactionTrigger;
    private bool ReactionAttackTrigger;
    private bool RitualCasting;
    private bool BookRitualOnly => CastSpell is { } spell && (ClassPlayRules.SpellSource(Current,spell.Id)?.Preparation.FromSpellbook??Current.Play.Preparation.FromSpellbook) && Current.Play.SpellbookIds.Contains(spell.Id) && !Current.Play.PreparedSpellIds.Contains(spell.Id) && !Current.Play.GrantedSpellIds.Contains(spell.Id);
    private bool FreeBaseAvailable => CastSpell is { } spell && (spell.Level==0 || spell.AtWill || RitualCasting || spell.RitualOnly || BookRitualOnly);
    private string? ActiveCastId;
    private CastOutcome? Cast => Current.Play.LastCast is { } c && c.Id == ActiveCastId ? c : null;
    private string SpellEffectDice = "";
    private CastingPreview? Casting => CastSpell is null ? null : SpellRules.Preview(Current, CastSpell.Id, PaymentLevel);
    private static int ResourceSlotLevel(ResourcePool pool) => pool.SlotLevel ?? (pool.Id.StartsWith("slot", StringComparison.Ordinal) && int.TryParse(pool.Id[4..],out var level) ? level : 0);
    private int PaymentLevel => CastPayment == "none" ? CastSpell?.Level ?? 0 : Current.Resources.FirstOrDefault(r => r.Id == CastPayment) is { } pool && ResourceSlotLevel(pool)>0 ? ResourceSlotLevel(pool) : CastSpell?.Level ?? 1;
    private IEnumerable<ResourcePool> Payments => CastSpell?.Level == 0 || RitualCasting || CastSpell?.RitualOnly is true || BookRitualOnly ? [] : Current.Resources.Where(r => r.Remaining > 0 && (CastSpell?.AllowsSlotCasting is true && ResourceSlotLevel(r) >= (CastSpell?.Level ?? 1) || r.Id == CastSpell?.GrantResourceId || r.Id == "hunters" && CastSpell?.Id == "hunters-mark"));
    private WorkspaceSnapshot? RestSource;
    private RestRequest RestDraft = new();
    private RestPreview? RestPreview;
    private readonly List<int> StagedHitDice = [];
    private readonly Dictionary<string,List<int>> StagedPoolDice = [];
    private List<SourcePreparationChange> RestSourceChanges = [];
    private string SelectedHitDiePoolId = "";
    private HitDiePool? SelectedHitDiePool=>Current.Play.HitDiePools.FirstOrDefault(p=>p.Id==SelectedHitDiePoolId);
    private int RestDieSize=>SelectedHitDiePool?.Sides??Current.Play.HitDieSize;
    private int SelectedDiceCount=>SelectedHitDiePool is null?StagedHitDice.Count:StagedPoolDice.GetValueOrDefault(SelectedHitDiePoolId)?.Count??0;
    private int? PhysicalHitDie;
    private bool ChangeSpells,ChangeCantrips;
    private List<string> ClassCantripDraft=[];
    private bool EndConcentration = true;
    private HunterMarkRetargetRequest MoveMarkDraft = new();
    private string MoveMarkTarget = "";
    private bool PreviousQuarryDefeated;
    private bool OwnsDread=>RangerRules.IsGloomStalker(Current);
    private bool CanDread=>OwnsDread&&Roll?.AttackKind==AttackKind.Weapon&&!Current.Play.DreadUsedThisTurn&&Current.Resources.Any(r=>r.Id=="dread"&&r.Remaining>0);
    private bool HasActiveMark => !string.IsNullOrWhiteSpace(Current.Play.HunterTarget) && Current.Effects.Any(effect=>effect.Concentration&&effect.Source=="spell:hunters-mark");
    private bool CanRest => Current.CurrentHp>0;
    private List<string> PreparedDraft = [];
    private int HitDiceAvailable => SelectedHitDiePool?.Remaining??Current.Resources.FirstOrDefault(r => r.Id == "hitdice")?.Remaining ?? 0;
    private string BoundCharacterId="";
    protected override void OnParametersSet()
    {
        if(BoundCharacterId!=Current.Id)
        {
            BoundCharacterId=Current.Id;CastSpell=null;ActiveCastId=null;ActiveRollId=null;RestSource=null;RestPreview=null;
            RollDraft=new();CastDraft=new();RestDraft=new();MoveMarkDraft=new();MoveMarkTarget="";PreviousQuarryDefeated=false;StagedHitDice.Clear();PreparedDraft=[];LocalError=null;
        }
        else if(CastSpell is not null&&!Current.Play.Spells.Any(s=>s.Id==CastSpell.Id))
        {
            CastSpell=null;ActiveCastId=null;
        }
    }
    private static string Signed(int n) => n >= 0 ? "+" + n : n.ToString();
    private static string DiceText(DiceFormula dice) => $"{dice.Count}d{dice.Sides}";
    private static string SpellName(CharacterState c, string id) => c.Play.Spells.FirstOrDefault(s => s.Id == id)?.Name ?? SpellCatalog.All.FirstOrDefault(s => s.Id == id)?.Name ?? id;
    private static List<int> DiceList(string text) => string.IsNullOrWhiteSpace(text) ? [] : text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    private Task CloseAsync(string id) => JS.InvokeVoidAsync("heroholdUi.closeDialog", id).AsTask();
    private async Task OpenAsync(string id) { LocalError = null; await InvokeAsync(StateHasChanged); await JS.InvokeVoidAsync("heroholdUi.openDialog", id); }
    private async Task RunAsync(Func<Task> action)
    {
        if (Working || Session.IsBusy) return;
        Working = true; LocalError = null;
        try { await action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or OverflowException) { LocalError = e.Message; }
        finally { Working = false; }
    }
    private bool IsDamageAttack(RollOutcome roll)=>roll.Kind==RollKind.Attack||roll.Kind==RollKind.SpellAttack&&Current.Combat.AttackSelection is {AttackProfile.Dice:not null} selection&&selection.RollId==roll.Id;
    private bool PreRollEffects=>Current.Combat.Enabled&&RollDraft.Kind==RollKind.Attack||RollDraft.Kind==RollKind.SpellAttack&&RollDraft.CombatContext?.CastId.Length>0&&HasActiveMark;
    public async Task OpenRollAsync(RollRequest request, Calculation standing, DiceFormula? weapon = null, string damageType = "Piercing", int? damageModifier = null, int? baseFixedDamage = null,DiceMode? receiptMode=null)
    {
        Mode=receiptMode??PreferredDiceMode;
        ReactionAttackTrigger=false;
        RollDraft = request with { OperationId = Guid.NewGuid().ToString("N"), SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision };
        Standing = standing; ActiveRollId = null; FlatBonus = QuickRoll?PresetFlat:0; BonusCount = QuickRoll?PresetCount:0; BonusSides = QuickRoll?PresetSides:4;
        Advantage = QuickRoll?PresetAdvantage:RollAdvantage.Normal; PhysicalD20 = ""; PhysicalBonus = ""; LuckyD20 = null;
        WeaponFixedDamage=baseFixedDamage; MarkDamage = false; DreadDamage = false; DamageDice = ""; WeaponDamage = weapon ?? new(1, 8); WeaponDamageType = damageType; WeaponModifier = damageModifier; DamageTarget = Current.KnownTarget.Name; CriticalHit = false;
        await OpenAsync("roll-dialog");
        if(QuickRoll&&Mode==DiceMode.Digital&&!PreRollEffects)await RollAsync();
    }
    public async Task ResumeRollAsync()
    {
        if(Current.Play.LastRoll is not {Resolved:false} roll)return;
        var profile=Current.Combat.AttackSelection?.AttackProfile;
        Mode=roll.Mode;RollDraft=new(){Name=roll.Name,Kind=roll.Kind,AttackKind=roll.AttackKind};Standing=roll.StandingBonus;ActiveRollId=roll.Id;
        WeaponDamage=profile?.Dice??new(1,8);WeaponModifier=profile?.Modifier??0;WeaponFixedDamage=profile?.FixedDamage;WeaponDamageType=profile?.DamageType??"Piercing";
        MarkDamage=false;DreadDamage=false;DamageDice="";DamageTarget=Current.Combat.AttackSelection?.Target??"";CriticalHit=roll.ChosenD20==20;
        await OpenAsync("roll-dialog");
    }
    public async Task ResumeCastAsync()
    {
        if(Current.Play.LastCast is not {} cast)return;
        Mode=cast.Mode;CastSpell=SpellCatalog.Find(Current,cast.Preview.SpellId);ActiveCastId=cast.Id;CastPayment=cast.PaymentResourceId;CastTarget=cast.Target;SpellEffectDice="";await OpenAsync("spell-cast-dialog");
    }
    private Task RollAsync() => RunAsync(async () =>
    {
        var context=RollDraft.CombatContext;
        if(context is not null&&(Current.Combat.Enabled&&RollDraft.Kind==RollKind.Attack||RollDraft.Kind==RollKind.SpellAttack))context=context with{TriggerConfirmed=context.ConfirmedFacts.Contains("reaction-attack")?ReactionAttackTrigger:context.TriggerConfirmed,ArmedEffects=[..(MarkDamage?new[]{"hunters-mark"}:[]),..(DreadDamage?new[]{"dreadful-strike"}:[])]};
        await Session.RollAsync(RollDraft with { CombatContext=context,Mode = Mode, Advantage = Advantage, FlatBonus = FlatBonus,
            BonusDice = BonusCount > 0 ? new(BonusCount, BonusSides) : null,
            PhysicalD20 = Mode == DiceMode.Physical ? DiceList(PhysicalD20) : [],
            PhysicalBonusDice = Mode == DiceMode.Physical ? DiceList(PhysicalBonus) : [] });
        if (Session.Error is null)
        {
            ActiveRollId = Session.Character?.Play.LastRoll?.Id;
            if(context?.ArmedEffects.Contains("hunters-mark")==true)DamageTarget=Current.Play.HunterTarget;
            if(QuickRoll){PresetAdvantage=RollAdvantage.Normal;PresetFlat=PresetCount=0;PresetSides=4;await OnRollOptionsChanged.InvokeAsync();}
        }
    });
    private Task LuckyAsync() => RunAsync(async () =>
    {
        if (Roll is null) return;
        await Session.UseLuckyAsync(new LuckyRequest { SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision, RollId = Roll.Id,
            PhysicalD20 = Roll.Mode == DiceMode.Physical ? LuckyD20 : null });
    });
    private Task ResolveAttackAsync(bool hit) => RunAsync(async () =>
    {
        if (Roll is null) return;
        await Session.ResolveAttackAsync(new AttackResolutionRequest { SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision,
            RollId = Roll.Id, Hit = hit, BaseDamage = WeaponDamage, BaseFixedDamage=WeaponFixedDamage, DamageType = WeaponDamageType,
            DamageModifier = WeaponModifier ?? CharacterCalculations.Ability(Current, RollDraft.Ability).Total, Target = DamageTarget, Critical = CriticalHit,
            HuntersMark = hit && MarkDamage, DreadfulStrike = hit && DreadDamage,
            PhysicalDamageDice = Roll.Mode == DiceMode.Physical && hit ? DiceList(DamageDice) : [] });
    });
    private Task InspirationApplied()=>InvokeAsync(StateHasChanged);
    private Task ConfirmRollAsync() => RunAsync(async () =>
    {
        if (Roll is null) return;
        await Session.ResolveRollAsync(new ResolveRollRequest { SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision, RollId = Roll.Id });
        if (Session.Error is null) await CloseAsync("roll-dialog");
    });
    public async Task OpenCastAsync(SpellDefinition spell,CombatActionContext? combatContext=null)
    {
        Mode=PreferredDiceMode;
        CastSpell = spell; RitualCasting=spell.RitualOnly || BookRitualOnly; CastPayment = FreeBaseAvailable ? "none" : Payments.FirstOrDefault()?.Id ?? ""; CastTarget = "";
        CastQualifyingHit = false; CastConcentration = false; CastReactionTrigger=false;ActiveCastId = null; SpellEffectDice = "";
        CastDraft = new CastRequest { CombatContext=combatContext,SpellId = spell.Id, SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision };
        await OpenAsync("spell-cast-dialog");
    }
    private async Task RollCastAttackAsync()
    {
        if(Cast is not {} cast||cast.AttacksRemaining is not>0||cast.Preview.Dice is null)return;
        var definition=SpellCatalog.Find(Current,cast.Preview.SpellId);var ability=definition.CastingAbility??Current.Play.SpellcastingAbility;
        await OpenRollAsync(new(){Name=cast.Preview.Name+" spell attack",Kind=RollKind.SpellAttack,AttackKind=AttackKind.Spell,Ability=ability,CombatContext=CombatRules.Context(Current,"spell:"+cast.Preview.SpellId)},CharacterCalculations.SpellAttack(Current,ability),cast.Preview.Dice,cast.Preview.DamageType??"",cast.Preview.Modifier,receiptMode:cast.Mode);
    }
    private async Task NextSpellAttackAsync(){await CloseAsync("roll-dialog");await RollCastAttackAsync();}
    private void ChangeRitual(ChangeEventArgs e){RitualCasting=e.Value is true;CastPayment=FreeBaseAvailable?"none":Payments.FirstOrDefault()?.Id??"";}
    private Task ConfirmCastAsync() => RunAsync(async () =>
    {
        if (CastSpell is null || Cast is not null) return;
        await Session.CastAsync(CastDraft with { CombatContext=CastDraft.CombatContext is {} ctx?ctx with{TriggerConfirmed=CastReactionTrigger}:null,SlotLevel = PaymentLevel, PaymentResourceId = CastPayment,
            Target = CastTarget, QualifyingHit = CastQualifyingHit, ConfirmConcentration = CastConcentration, Mode = Mode, Ritual = RitualCasting || CastSpell.RitualOnly });
        if (Session.Error is null) ActiveCastId = Session.Character?.Play.LastCast?.Id;
    });
    private Task ResolveSpellAsync() => RunAsync(async () =>
    {
        if (Cast is null) return;
        await Session.ResolveCastEffectAsync(new CastEffectRequest { SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision,
            CastId = Cast.Id, PhysicalDice = Cast.Mode == DiceMode.Physical ? DiceList(SpellEffectDice) : [] });
    });
    public async Task OpenRestAsync(RestKind kind)
    {
        Mode=PreferredDiceMode;
        RestSource = Session.Snapshot; RestDraft = new RestRequest { Kind = kind, SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision };
        StagedHitDice.Clear(); StagedPoolDice.Clear();RestSourceChanges=[];SelectedHitDiePoolId=Current.Play.HitDiePools.FirstOrDefault()?.Id??""; PhysicalHitDie = null; ChangeSpells = false;ChangeCantrips=false;ClassCantripDraft=[..Current.Play.Preparation.OwnedCantripSpellIds]; EndConcentration = true;
        PreparedDraft = [.. Current.Play.PreparedSpellIds]; RestPreview = null;
        await OpenAsync("rest-dialog");
        StageRest();await InvokeAsync(StateHasChanged);
    }
    private void StageRest()
    {
        if (RestSource is null) return;
        try
        {
            RestPreview = RestRules.Preview(RestSource, BuildRestRequest());
            LocalError = null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { LocalError = e.Message; RestPreview = null; }
    }
    private void AddHitDie()
    {
        if (!CanRest) { LocalError="A Short or Long Rest must start with at least 1 Hit Point. Resolve recovery from zero HP first.";return; }
        if (SelectedDiceCount >= HitDiceAvailable) return;
        var die = Mode == DiceMode.Digital ? RandomNumberGenerator.GetInt32(1, RestDieSize + 1) : PhysicalHitDie;
        if (die is null || die < 1 || die > RestDieSize) { LocalError = "Enter a valid unmodified Hit Die result first."; return; }
        if(SelectedHitDiePool is not null){if(!StagedPoolDice.TryGetValue(SelectedHitDiePoolId,out var values)){values=[];StagedPoolDice.Add(SelectedHitDiePoolId,values);}values.Add(die.Value);}
        else StagedHitDice.Add(die.Value);
        PhysicalHitDie = null; StageRest();
    }
    private void SelectHitDiePool(ChangeEventArgs e){SelectedHitDiePoolId=e.Value?.ToString()??"";PhysicalHitDie=null;StageRest();}
    private void SetSourceRestChanges(List<SourcePreparationChange> changes){RestSourceChanges=changes;StageRest();}
    private RestRequest BuildRestRequest()=>RestDraft with { Mode=DiceMode.Physical,
        HitDiceToSpend=Current.Play.HitDiePools.Count==0?StagedHitDice.Count:0,PhysicalHitDice=Current.Play.HitDiePools.Count==0?[..StagedHitDice]:[],
        HitDieSpends=StagedPoolDice.Select(pair=>new HitDieSpend{PoolId=pair.Key,Count=pair.Value.Count,PhysicalDice=[..pair.Value]}).ToList(),
        SourceChanges=RestSourceChanges,EndConcentration=EndConcentration,ChangeCantrips=ChangeCantrips,NewClassCantripSpellIds=ChangeCantrips?[..ClassCantripDraft]:[],ChangeSpells=ChangeSpells,NewPreparedSpellIds=ChangeSpells?[..PreparedDraft]:[] };
    private void ToggleSpellChanges(ChangeEventArgs e)
    {
        ChangeSpells = e.Value is true; PreparedDraft = [.. Current.Play.PreparedSpellIds]; StageRest();
    }
    private void ToggleCantripChanges(ChangeEventArgs e){ChangeCantrips=e.Value is true;ClassCantripDraft=[..Current.Play.Preparation.OwnedCantripSpellIds];StageRest();}
    private void ToggleClassCantrip(string id,ChangeEventArgs e){if(e.Value is true){if(!ClassCantripDraft.Contains(id))ClassCantripDraft.Add(id);}else ClassCantripDraft.Remove(id);StageRest();}
    private void TogglePrepared(string id, ChangeEventArgs e)
    {
        if (e.Value is true) { if (!PreparedDraft.Contains(id)) PreparedDraft.Add(id); }
        else PreparedDraft.Remove(id);
        StageRest();
    }
    private Task ConfirmRestAsync() => RunAsync(async () =>
    {
        if (RestPreview is null) StageRest();
        if (RestPreview is null) return;
        await Session.RestAsync(BuildRestRequest());
        if (Session.Error is null) await CloseAsync("rest-dialog");
    });
    public async Task OpenMoveMarkAsync()
    {
        MoveMarkDraft=new(){CombatContext=Current.Combat.EncounterActive?CombatRules.Context(Current,"spell:hunters-mark-retarget"):null,SourceCharacterId=Current.Id,SourceRevision=Session.Snapshot!.Revision};MoveMarkTarget="";PreviousQuarryDefeated=false;
        await OpenAsync("move-mark-dialog");
    }
    private Task ConfirmMoveMarkAsync()=>RunAsync(async()=>
    {
        await Session.RetargetHuntersMarkAsync(MoveMarkDraft with{Target=MoveMarkTarget,PreviousTargetAtZeroHp=PreviousQuarryDefeated});
        if(Session.Error is null)await CloseAsync("move-mark-dialog");
    });
    public Task UndoAsync() => RunAsync(async () =>
    {
        await Session.UndoAsync(new UndoRequest { SourceCharacterId = Current.Id, SourceRevision = Session.Snapshot!.Revision });
        if (Session.Error is null) { ActiveCastId = null; ActiveRollId = null; }
    });
}
