using Herohold.Core;
using Herohold.Web.Services;
using Herohold.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json.Nodes;

namespace Herohold.Web.Components.Pages;

public partial class Home
{
    private string View { get; set; } = "adventure";
    [Inject] private IServiceProvider Services { get; set; } = default!;
    private HeroholdFeatures Features => Services.GetService(typeof(HeroholdFeatures)) as HeroholdFeatures ?? HeroholdFeatures.Default;
    /// <summary>Combat screens show only when the feature is on and the character is in combat.</summary>
    private bool CombatActive(CharacterState c) => Features.Combat && c.Combat.Enabled;
    private string Theme { get; set; } = "system";
    private string SpellSearch { get; set; } = "";
    private string SpellLevelFilter { get; set; } = "all";
    private ItemUseDialog? ItemUse;
    private Task OpenItemUse(EquipmentItem item)=>ItemUse?.OpenAsync(item)??Task.CompletedTask;
    private ThrowDialog? ThrowItem;
    private ScrollDialog? ReadScroll;
    private Task OpenScroll(EquipmentItem item)=>ReadScroll?.OpenAsync(item)??Task.CompletedTask;
    private Task OpenThrow(EquipmentItem item)=>ThrowItem?.OpenAsync(item)??Task.CompletedTask;
    /// <summary>Carried items thrown in place of an attack (Acid, Alchemist's Fire, Net).</summary>
    private IEnumerable<EquipmentItem> ThrownItems=>Character?.Equipment.Where(i=>i.ThrownRangeFt is not null&&i.Carried&&i.Quantity>0)??[];
    private static string ThrownSummary(CharacterState c,EquipmentItem item)=>$"DC {ThrownItemRules.SaveDc(c).Total} Dex save · {item.ThrownRangeFt} ft"+(item.ThrownDamage is not null?" · "+ItemLinking.ThrownDamageText(item):"");
    private AdventureDialog? Adventure;
    private HealthDialog? Health;
    private Task OpenAdventure()=>Adventure?.OpenAsync()??Task.CompletedTask;
    private static string HealthTone(CharacterState c)=>c.CurrentHp==0?"health-empty":c.CurrentHp*4<=c.MaximumHp?"health-critical":c.CurrentHp*2<=c.MaximumHp?"health-low":"health-full";
    private SpellInspector? SpellDetails;
    private ProfileDialog? Profile;
    private PurseDialog? Purse;
    private SurvivalDialog? Survival;
    private OwnedReferencesDialog? References;
    private DiceMode PreferredDiceMode=DiceMode.Digital;
    private bool Interactive { get; set; }
    private WorkspaceDialogs? Dialogs;
    private CharacterState? Character => Session.Character;
    private bool IsSample => Character?.Id == SampleWorkspace.CharacterId;
    private static readonly (string Key, string Title, string Symbol)[] Destinations =
    [ ("adventure", "Play", "sheet"), ("equipment", "Inventory", "pack"), ("companions", "Travel & companions", "compass"), ("journal", "Journal", "book") ];
    private Task OpenSpellLibrary()=>JS.InvokeVoidAsync("heroholdUi.openDialog","spell-library-dialog").AsTask();
    private static readonly (string Name,Ability Ability)[] AllSkills = [("Acrobatics",Ability.Dexterity),("Animal Handling",Ability.Wisdom),("Arcana",Ability.Intelligence),("Athletics",Ability.Strength),("Deception",Ability.Charisma),("History",Ability.Intelligence),("Insight",Ability.Wisdom),("Intimidation",Ability.Charisma),("Investigation",Ability.Intelligence),("Medicine",Ability.Wisdom),("Nature",Ability.Intelligence),("Perception",Ability.Wisdom),("Performance",Ability.Charisma),("Persuasion",Ability.Charisma),("Religion",Ability.Intelligence),("Sleight of Hand",Ability.Dexterity),("Stealth",Ability.Dexterity),("Survival",Ability.Wisdom)];
    private static string SkillKey(string name)=>name.ToLowerInvariant().Replace(' ','-');
    private static bool HasPassiveReference(string name)=>name is "Perception" or "Insight" or "Investigation";
    private static Calculation PassiveSkill(CharacterState character,string name,Ability ability)
    {var skill=CharacterCalculations.Skill(character,name,ability);return new(10+skill.Total,[new("Passive base",10),..skill.Terms]);}
    private static string ConditionSummary(CharacterState character)
    {
        var conditions=SheetRules.EffectiveEffects(character).Select(effect=>effect.Name).ToList();
        if(character.Exhaustion>0)conditions.Add($"Exhaustion {character.Exhaustion}");
        return conditions.Count==0?"No conditions":string.Join(" · ",conditions);
    }
    private static string ClassSummary(CharacterState c)=>c.Build?.ClassLevels is {Count:>1} levels?string.Join(" / ",levels.Select(p=>p.Key+" "+p.Value)):c.ClassName+" "+c.Level;
    private string Initials=>string.Concat((Character?.Name??"Character").Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part=>char.ToUpperInvariant(part[0])));
    private string Senses
    {
        get
        {
            if(Character?.Derived.Senses is {Count:>0} senses)return string.Join(" · ",senses.Select(s=>s.Range>0?s.Type+" "+s.Range+" ft":s.Type));
            return IsSample?"Darkvision":"No special senses recorded";
        }
    }
    private static string Readable(JsonNode? value) => value switch { JsonArray array => string.Join(", ", array.Select(Readable)), JsonObject obj => string.Join(" · ",obj.Select(p=>p.Key+": "+Readable(p.Value))),JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,_=>value?.ToJsonString()??"" };
    private InventoryDefenseDialogs? InventoryDialogs;
    private Task OpenItem(EquipmentItem? item = null) => InventoryDialogs?.OpenItemAsync(item) ?? Task.CompletedTask;
    private Task OpenDefenses(bool target = false) => InventoryDialogs?.OpenDefensesAsync(target) ?? Task.CompletedTask;
    private sealed record AttackAction(string Key,string Name,Ability Ability,bool Proficient,int Flat,string Summary,DiceFormula? Dice,int DamageModifier,string DamageType,int? FixedDamage=null,AttackKind Kind=AttackKind.Weapon,string Ammunition="")
    {
        public string Damage => Dice is { } dice?$"{dice.Count}d{dice.Sides}{Signed(DamageModifier)}":$"{FixedDamage??0}{Signed(DamageModifier)}";
    }
    private IEnumerable<AttackAction> Attacks
    {
        get
        {
            if(Character is null)yield break;
            if(Character.Derived.Attacks.Count>0)
            {
                foreach(var entry in Character.Derived.Attacks)
                {
                    if(entry.Kind!="unarmed"&&!Character.Equipment.Any(i=>i.Quantity>0&&i.Equipped&&i.Name.Equals(entry.Name,StringComparison.OrdinalIgnoreCase)))continue;
                    yield return new(entry.Id.Replace("weapon:","",StringComparison.Ordinal),entry.Name,entry.Ability,entry.Proficient,entry.FlatBonus,entry.Range+" · "+entry.Notes,entry.DamageDice,
                        CharacterCalculations.Ability(Character,entry.Ability).Total+entry.DamageModifierAdjustment,entry.DamageType,entry.FixedDamage,entry.Kind switch{"weapon"=>AttackKind.Weapon,"unarmed"=>AttackKind.Unarmed,"spell"=>AttackKind.Spell,_=>AttackKind.Unknown},entry.Ammunition);
                }
                yield break;
            }
            if(Character.Id!=SampleWorkspace.CharacterId)yield break;
            if(Character.Equipment.Any(i=>i.Name=="Longbow"&&i.Quantity>0&&i.Equipped))yield return new("longbow","Longbow",Ability.Dexterity,true,2,"150 / 600 ft · Archery · Slow mastery",new(1,8),CharacterCalculations.Ability(Character,Ability.Dexterity).Total,"Piercing");
            if(Character.Equipment.Any(i=>i.Name=="Shortsword"&&i.Quantity>0&&i.Equipped))yield return new("shortsword","Shortsword",Ability.Dexterity,true,0,"5 ft · Finesse · Vex mastery",new(1,6),CharacterCalculations.Ability(Character,Ability.Dexterity).Total,"Piercing");
        }
    }
    /// <summary>Ammunition left for an attack while counting is on ("Arrows 18"), or null.</summary>
    private string? AmmunitionLeft(AttackAction attack)=>Character is {Sheet.CountAmmunition:true} c&&attack.Ammunition.Length>0?$"{AmmunitionRules.Plural(attack.Ammunition)} {AmmunitionRules.Carried(c,attack.Ammunition)}":null;
    private bool OutOfAmmunition(AttackAction attack)=>Character is {Sheet.CountAmmunition:true} c&&attack.Ammunition.Length>0&&AmmunitionRules.Carried(c,attack.Ammunition)==0;
    private IEnumerable<SpellDefinition> Spells => Character is null ? [] : Character.Play.Spells.Where(s => Character.Play.PreparedSpellIds.Concat(Character.Play.GrantedSpellIds).Concat(Character.Play.KnownSpellIds).Contains(s.Id) || Character.Play.Preparation.FromSpellbook && s.Ritual && Character.Play.SpellbookIds.Contains(s.Id));
    private GameplayDialogs? Gameplay;
    private CombatWorkspace? CombatPanel;
    private bool ImportOpen;
    private bool ShowWorkflow;
    private string WorkflowKey="";
    private string? SelectionNotice;
    private void ResumeWorkflow()=>ShowWorkflow=true;
    private static string SpellSummary(SpellDefinition spell) => $"{SpellText.CastingTime(spell.CastingTime)} · {spell.Range} · {spell.Source}" + (spell.Concentration ? " · Concentration" : "") + (spell.DamageType is { Length: > 0 } type ? " · " + type : "");
    private IEnumerable<SpellDefinition> FilteredSpells=>Spells.Where(s=>(string.IsNullOrWhiteSpace(SpellSearch)||s.Name.Contains(SpellSearch,StringComparison.OrdinalIgnoreCase))&&(SpellLevelFilter=="all"||s.Level.ToString()==SpellLevelFilter)).OrderBy(s=>s.Level).ThenBy(s=>s.Name,StringComparer.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        Session.Changed += SessionChanged;
        await Session.InitializeAsync();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Theme = await JS.InvokeAsync<string>("heroholdUi.initializeTheme");
            var selected=await JS.InvokeAsync<string>("heroholdUi.selectedCharacter");
            if(!string.IsNullOrWhiteSpace(selected)&&selected!=Character?.Id)
            {
                await Session.SwitchCharacterAsync(selected);
                if(Session.Error is not null){SelectionNotice="Your previous character is not in this workspace. Choose one from the library.";await Session.ReloadAsync();}
            }
            if(Character is not null)await JS.InvokeVoidAsync("heroholdUi.rememberCharacter",Character.Id);
            Interactive = true;
            await InvokeAsync(StateHasChanged);
        }
    }
    private void SessionChanged() => _ = InvokeAsync(() => { var key=Session.Workflow?.Stamp??""; if(key!=WorkflowKey) { WorkflowKey=key; if(key.Length>0)ShowWorkflow=true; } StateHasChanged(); });
    private async Task Navigate(string view) { await CloseMenus(); if(view=="journal"){await JS.InvokeVoidAsync("heroholdUi.openDialog","journal-sheet-dialog");return;} View = view; ShowWorkflow=false; if (view == "library") { await CloseDialog("app-drawer"); await Session.RefreshLibraryAsync(); } }
    private async Task Reload() { await CloseMenus(); await Session.ReloadAsync(); }
    private async Task ChangeTheme(ChangeEventArgs e)
    {
        Theme = e.Value?.ToString() ?? "system";
        await JS.InvokeVoidAsync("heroholdUi.applyTheme", Theme);
    }
    private Task CloseMenus()=>JS.InvokeVoidAsync("heroholdUi.closeMenus").AsTask();
    private Task OpenNote()=>OpenJournalNote(JournalCategory.Notes);
    private async Task OpenJournalNote(JournalCategory category){await CloseMenus();if(Dialogs is not null)await Dialogs.OpenNoteAsync(category);}
    private async Task OpenJournalNote(string category){await CloseMenus();if(Dialogs is not null)await Dialogs.OpenNoteAsync(category);}
    private async Task OpenEffects(){await CloseMenus();if(Dialogs is not null)await Dialogs.OpenEffectsAsync();}
    private async Task OpenDetails(){await CloseMenus();if(Dialogs is not null)await Dialogs.OpenDetailsAsync();}
    private async Task OpenProfile(){await CloseMenus();if(Profile is not null)await Profile.OpenAsync();}
    private async Task OpenPurse(){await CloseMenus();if(Purse is not null)await Purse.OpenAsync();}
    private async Task OpenSurvival(){await CloseMenus();if(Survival is not null)await Survival.OpenAsync();}
    private async Task OpenReferences(){await CloseMenus();if(References is not null)await References.OpenAsync();}
    private static string SkillTraining(CharacterState c,string name)=>c.Expertise.Contains(name,StringComparer.OrdinalIgnoreCase)?"Expertise":c.SkillProficiencies.Contains(name,StringComparer.OrdinalIgnoreCase)?"Proficient":"Untrained";
    private async Task OpenHealth(HealthChangeKind kind){await CloseMenus();if(Health is not null)await Health.OpenAsync(kind);}
    private Task OpenHealingFromSpell(int amount)=>Health?.OpenAsync(HealthChangeKind.Healing,amount)??Task.CompletedTask;
    private Task AdvanceOwnTurn()=>Session.Snapshot is { } source?Session.AdvanceOwnTurnAsync(new(){SourceCharacterId=source.Character.Id,SourceRevision=source.Revision}):Task.CompletedTask;
    private Task RollInitiative()=>OpenRoll("Initiative",CharacterCalculations.Initiative(Character!),RollKind.Initiative,Ability.Dexterity);
    private Task RollAbility(Ability ability)=>OpenRoll(ability+" check",CharacterCalculations.AbilityCheck(Character!,ability),RollKind.Ability,ability);
    private Task RollSpellAttack(SpellAttackChoice choice)=>Gameplay?.OpenRollAsync(new(){Name=choice.Source+" spell attack",Kind=RollKind.SpellAttack,Ability=choice.Ability},CharacterCalculations.SpellAttack(Character!,choice.Ability))??Task.CompletedTask;
    private Task OpenRoll(string title, Calculation value, RollKind kind, Ability ability, string skill = "", int standingFlat = 0, DiceFormula? weapon = null) => Gameplay?.OpenRollAsync(new RollRequest { Name = title, Kind = kind, Ability = ability, Skill = skill, StandingFlatBonus = standingFlat }, value, weapon) ?? Task.CompletedTask;
    private Task OpenNextRollOptions()=>Gameplay?.OpenRollOptionsAsync()??Task.CompletedTask;
    private void RefreshRollOptions()=>StateHasChanged();
    private Task OpenAttack(AttackAction attack) => OpenCombatAttack(attack,false);
    private Task OpenReactionAttack(AttackAction attack)=>OpenCombatAttack(attack,true);
    private Task OpenCombatAttack(AttackAction attack,bool reaction) => Gameplay?.OpenRollAsync(new RollRequest { CombatContext=CombatRules.Context(Character!,"attack:"+attack.Key) with{ConfirmedFacts=reaction?["reaction-attack"]:[],AttackProfile=new(){Name=attack.Name,Dice=attack.Dice,Modifier=attack.DamageModifier,FixedDamage=attack.FixedDamage,DamageType=attack.DamageType}},Name = attack.Name + " attack", Kind = RollKind.Attack, AttackKind=attack.Kind, Ability = attack.Ability, Proficient = attack.Proficient, StandingFlatBonus = attack.Flat }, CharacterCalculations.Attack(Character!,attack.Ability,attack.Proficient,attack.Flat),attack.Dice,attack.DamageType,attack.DamageModifier,attack.FixedDamage) ?? Task.CompletedTask;
    private Task InspectSpell(SpellDefinition spell)=>SpellDetails?.OpenAsync(spell)??Task.CompletedTask;
    private Task CastSpell(SpellDefinition spell) => Gameplay?.OpenCastAsync(spell,Character!.Combat.Enabled?CombatRules.Context(Character,"spell:"+spell.Id):null) ?? Task.CompletedTask;
    private Task TransitionCombat(CombatTransitionRequest request)=>Session.Snapshot is {} source?Session.TransitionCombatAsync(request with{SourceCharacterId=source.Character.Id,SourceRevision=source.Revision}):Task.CompletedTask;
    private Task CombatSkill((string Name,Ability Ability) skill)=>OpenRoll(skill.Name+" check",CharacterCalculations.Skill(Character!,skill.Name,skill.Ability),RollKind.Skill,skill.Ability,skill.Name);
    private Task CombatSave(Ability ability)=>OpenRoll(ability+" saving throw",CharacterCalculations.Save(Character!,ability),RollKind.Save,ability);
    private Task CombatItem(EquipmentItem item)=>Navigate("equipment");
    private Task ResumeCombatRoll()=>Gameplay?.ResumeRollAsync()??Task.CompletedTask;
    private Task ResumeCombatCast()=>Gameplay?.ResumeCastAsync()??Task.CompletedTask;
    private async Task Rest(RestKind kind){await CloseMenus();if(Gameplay is not null)await Gameplay.OpenRestAsync(kind);}
    private Task MoveMark() => Gameplay?.OpenMoveMarkAsync() ?? Task.CompletedTask;
    private Task Undo() => Gameplay?.UndoAsync() ?? Task.CompletedTask;
    private async Task OpenImport() { ImportOpen = true; await Navigate("library"); }
    private async Task Export(string format) { await CloseMenus(); var artifact = await Session.ExportAsync(format); if (artifact is not null) await JS.InvokeVoidAsync("heroholdUi.download", artifact.FileName, artifact.MediaType, Convert.ToBase64String(artifact.Bytes)); }
    private async Task WorkflowFinished() { View = "adventure"; ShowWorkflow=false; SelectionNotice=null; if(Character is not null)await JS.InvokeVoidAsync("heroholdUi.rememberCharacter",Character.Id); await Session.RefreshLibraryAsync(); }

    private Task CloseDialog(string id) => JS.InvokeVoidAsync("heroholdUi.closeDialog", id).AsTask();
    private Task RemoveEffect(string id) => Session.RemoveEffectAsync(id);
    private Task AdjustHitDiePool((string Id,int Delta,string SourceId) value)=>Session.AdjustHitDiePoolAsync(value.Id,value.Delta,value.SourceId);
    private Task AdjustResource((string Id, int Delta, string SourceId) value) => Session.AdjustResourceAsync(value.Id, value.Delta, value.SourceId);
    private static string Short(Ability ability) => ability.ToString()[..3].ToUpperInvariant();
    private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    private static bool Checked(ChangeEventArgs e) => e.Value is bool value && value;
    private static string ItemKey(EquipmentItem item) => item.Name.Contains("potion", StringComparison.OrdinalIgnoreCase) ? "potion" : item.Id;
    public void Dispose() => Session.Changed -= SessionChanged;
}
