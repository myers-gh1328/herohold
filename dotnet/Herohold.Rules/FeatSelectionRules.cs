using Herohold.Core;
namespace Herohold.Rules;

public sealed class FeatSelectionRules
{
    private readonly IReadOnlyList<CatalogFeat> feats;
    public IReadOnlyList<CatalogFeat> AvailableDefinitions=>feats;
    private readonly IReadOnlyList<CatalogSpell> spells;
    private readonly ToolCatalog? tools;
    private static readonly string[] Skills=["Acrobatics","Animal Handling","Arcana","Athletics","Deception","History","Insight","Intimidation","Investigation","Medicine","Nature","Perception","Performance","Persuasion","Religion","Sleight of Hand","Stealth","Survival"];
    private static readonly string[] Rituals=["Alarm","Comprehend Languages","Detect Magic","Detect Poison and Disease","Find Familiar","Identify","Illusory Script","Purify Food and Drink","Speak with Animals","Tenser's Floating Disk","Unseen Servant"];
    public FeatSelectionRules(RulesCatalogSnapshot catalog, IReadOnlyList<CatalogFeat>? extraDefinitions=null, ToolCatalog? tools=null)
    {
        if(extraDefinitions is not null&&(extraDefinitions.Count>100||extraDefinitions.Any(d=>d.Description.Length>65536)))throw new ArgumentException("Additional feat definitions exceed their supported bounds.");
        feats=[..catalog.FeatRecords.Concat(extraDefinitions??[])];
        if(feats.Select(d=>d.Id).Distinct(StringComparer.Ordinal).Count()!=feats.Count)throw new ArgumentException("Feat identities must be unique.");
        spells=catalog.SpellRecords;this.tools=tools;
    }
    private CatalogFeat? Find(string id)=>feats.FirstOrDefault(f=>f.Id==id);
    private static BuildIssue Issue(string id,string code,string message,string? field=null)=>new(code,field is null?["general_feats",id]:["general_feats",id,field],message);
    private static IEnumerable<OwnedFeatSelection> Others(FeatRuleContext c)=>c.OwnedFeats.Where(f=>c.EditingSourceId.Length==0||f.SourceId!=c.EditingSourceId);
    private static HashSet<string> Proficiencies(FeatRuleContext c,ProficiencySourceKind kind)=>c.ProficiencySources.Where(p=>p.Kind==kind&&(c.EditingSourceId.Length==0||p.SourceId!=c.EditingSourceId)).Select(p=>p.Target).ToHashSet(StringComparer.Ordinal);
    public FeatEligibility Evaluate(string featId,FeatRuleContext context)
    {
        var feat=Find(featId);var issues=new List<BuildIssue>();
        if(feat is null)return new(false,false,[Issue(featId,"unknown_feat","Choose a known feat.")]);
        if(!feat.Repeatable&&Others(context).Any(f=>f.FeatId==featId||f.Name==feat.Name))issues.Add(Issue(featId,"already_owned","This feat cannot be selected more than once."));
        if(feat.Prerequisites.Any is not {} groups||!groups.Any(g=>GroupMet(g,context)))issues.Add(Issue(featId,"prerequisites","The feat prerequisites are not met."));
        var eligible=issues.Count==0;
        if(feat.Name=="Ability Score Improvement")issues.Add(Issue(featId,"use_asi_option","Choose Ability Score Improvement in the improvement step."));
        if(feat.RequiredChoices.Count>0&&DescribeChoices(featId,context).Count==0)issues.Add(Issue(featId,"unsupported_choice","This feat requires a choice that is not available yet."));
        var fields=DescribeChoices(featId,context);
        if(fields.Any(field=>field.Options.Count<field.Choose&&!(feat.Name=="Skill Expert"&&field.Key=="expertise"&&fields.Any(f=>f.Key=="skill_proficiency"&&f.Options.Count>0)))
            ||feat.Name=="Resilient"&&Proficiencies(context,ProficiencySourceKind.Save).Count>=6)
            issues.Add(Issue(featId,"no_available_choices","No eligible choices remain for this feat."));
        return new(eligible,issues.Count==0,issues);
    }
    private static bool GroupMet(FeatPrerequisiteGroup group,FeatRuleContext c)
    {
        if(c.Level<group.MinimumLevel||group.Spellcasting&&!c.HasSpellcastingFeature)return false;
        if(group.AbilityScores.Count>0&&!group.AbilityScores.Any(option=>option.All(p=>c.Scores.GetValueOrDefault(p.Key)>=p.Value)))return false;
        if(group.Other.Count>0)return false;
        foreach(var p in group.Proficiencies)
        {
            if(!Enum.TryParse<ProficiencySourceKind>(p.Type,true,out var kind)||!Proficiencies(c,kind).Contains(p.Value))return false;
        }
        var owned=c.Features.Concat(Others(c).Select(f=>f.Name)).ToHashSet(StringComparer.Ordinal);
        return group.Features.All(owned.Contains);
    }
    public IReadOnlyList<FeatChoiceField> DescribeChoices(string featId,FeatRuleContext context)
    {
        var definition=Find(featId);var name=definition?.Name??"";var fields=new List<FeatChoiceField>();
        void Field(string key,string label,IEnumerable<string> options,int choose=1)=>fields.Add(new(key,label,choose==1?BuildChoiceMode.Single:BuildChoiceMode.Multi,choose,options.Distinct(StringComparer.Ordinal).ToArray()));
        if(definition?.MagicInitiateClass is {Length:>0} spellClass)
        {
            Field("spellcasting_ability","Spellcasting ability",["Intelligence","Wisdom","Charisma"]);
            var eligible=spells.Where(s=>s.Classes.Contains(spellClass,StringComparer.Ordinal));
            Field("cantrips","Cantrips",eligible.Where(s=>s.Level==0).Select(s=>s.Name).Order(StringComparer.Ordinal),2);
            Field("spell","Level 1 spell",eligible.Where(s=>s.Level==1).Select(s=>s.Name).Order(StringComparer.Ordinal));
            return fields.AsReadOnly();
        }
        var skills=Proficiencies(context,ProficiencySourceKind.Skill);var expertise=Proficiencies(context,ProficiencySourceKind.Expertise);
        switch(name)
        {
            case "Skilled":Field("proficiencies","Skills or tools",Skills.Select(s=>"Skill: "+s).Concat((tools?.All??[]).Select(t=>"Tool: "+t)),3);break;
            case "Musician":Field("tools","Musical instruments",tools?.MusicalInstruments??[],3);break;
            case "Crafter":
                var crafting=(definition?.ToolChoices??[]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                Field("tools","Artisan tools",(tools?.ArtisanTools??[]).Where(crafting.Contains),3);break;
            case "Elemental Adept":
                var used=Others(context).Where(f=>f.Name==name).Select(f=>ChoiceReading.TextOf(f.Choices.Get("damage_type"))).ToHashSet();
                Field("damage_type","Damage type",new[]{"Acid","Cold","Fire","Lightning","Thunder"}.Where(x=>!used.Contains(x)));break;
            case "Skill Expert":
                var available=Skills.Where(s=>!skills.Contains(s)).ToArray();
                Field("skill_proficiency","Skill proficiency",available.Length>0?available:Skills);
                Field("expertise","Expertise",skills.Where(s=>!expertise.Contains(s)));break;
            case "Keen Mind":Field("skill","Skill",["Arcana","History","Investigation","Nature","Religion"]);break;
            case "Observant":Field("skill","Skill",["Insight","Investigation","Perception"]);break;
            case "Fey-Touched":case "Shadow-Touched":
                var schools=name=="Fey-Touched"?new[]{"D","E"}:new[]{"I","N"};
                Field("spell","Level 1 spell",spells.Where(s=>s.Level==1&&schools.Contains(s.SchoolCode)).Select(s=>s.Name).Order(StringComparer.Ordinal));break;
            case "Ritual Caster":
                var known=Others(context).Where(f=>f.Name==name).SelectMany(f=>ChoiceReading.Names(f.Choices.Get("spells"))).ToArray();var spellNames=spells.Where(s=>s.Level==1&&s.Ritual).Select(s=>s.Name).ToHashSet();
                var ordered=Rituals.Where(spellNames.Contains).Concat(spellNames.Except(Rituals,StringComparer.Ordinal).Order(StringComparer.Ordinal));
                fields.Add(new("spells","Level 1 ritual spells",BuildChoiceMode.Multi,Math.Max(1,2+(context.Level-1)/4-known.Length),ordered.Where(s=>!known.Contains(s)).ToArray()));break;
            case "Weapon Master":Field("weapon_mastery","Weapon Mastery",context.EligibleWeaponMasteries.Except(context.ExistingWeaponMasteries,StringComparer.Ordinal));break;
            case "Boon of Skill":Field("expertise","Expertise",Skills.Where(s=>!expertise.Contains(s)));break;
            case "Boon of Energy Resistance":Field("damage_types","Damage resistances",["Acid","Cold","Fire","Lightning","Necrotic","Poison","Psychic","Radiant","Thunder"],2);break;
        }
        return fields.AsReadOnly();
    }
    /// <summary>Checks a feat's choices (field → name or list of names) and its ability increase allocation (ability → amount).</summary>
    public FeatSelectionVerdict ValidateSelection(string featId,FeatRuleContext context,IReadOnlyDictionary<string,ChoiceValue>? selected=null,IReadOnlyDictionary<string,int>? abilityIncreases=null)
    {
        var issues=Evaluate(featId,context).Issues.ToList();var feat=Find(featId);if(feat is null)return new(false,issues);
        var choices=new Dictionary<string,ChoiceValue>(selected??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal);
        if(feat.Name=="Skilled"&&!choices.ContainsKey("proficiencies")&&(choices.ContainsKey("skills")||choices.ContainsKey("tools")))
        {
            if(choices.Get("skills") is {Items:null}||choices.Get("tools") is {Items:null})issues.Add(Issue(featId,"invalid_shape","Skilled selections must be lists."));
            choices["proficiencies"]=ChoiceValue.ListOf(ChoiceReading.Names(choices.Get("skills")).Select(s=>"Skill: "+s).Concat(ChoiceReading.Names(choices.Get("tools")).Select(s=>"Tool: "+s)).Select(ChoiceValue.Of));choices.Remove("skills");choices.Remove("tools");
        }
        var fields=DescribeChoices(featId,context);
        foreach(var field in fields)
        {
            var options=field.Options.ToHashSet(StringComparer.Ordinal);
            var skillProficiency=ChoiceReading.TextOf(choices.Get("skill_proficiency"));
            if(feat.Name=="Skill Expert"&&field.Key=="expertise"&&!Proficiencies(context,ProficiencySourceKind.Expertise).Contains(skillProficiency))options.Add(skillProficiency);
            var value=choices.Get(field.Key);bool valid;
            if(field.Mode==BuildChoiceMode.Multi)valid=value?.Items is {} a&&a.Count==field.Choose&&a.All(n=>n.Text is {} s&&options.Contains(s))&&a.Select(n=>n.Text).Distinct(StringComparer.Ordinal).Count()==a.Count;
            else valid=(value?.Text??"").Length>0&&options.Contains(value!.Text!);
            if(!valid)issues.Add(Issue(featId,"invalid_choice","Choose the required number of distinct eligible options.",field.Key));
        }
        foreach(var key in choices.Keys.Except(fields.Select(f=>f.Key),StringComparer.Ordinal))issues.Add(Issue(featId,"unknown_choice","This choice does not belong to the selected feat.",key));
        var allocation=abilityIncreases??new Dictionary<string,int>();var increase=feat.AbilityIncrease;
        var modes=increase is null?[]:increase.Mode=="multi_mode"?increase.Modes.ToArray():new[]{increase};
        bool Matches(FeatAbilityIncrease mode)=>allocation.Count==(mode.Choose??1)&&allocation.All(p=>mode.From.Contains(p.Key,StringComparer.Ordinal)&&p.Value==(mode.Amount??1));
        if(modes.Length>0&&!modes.Any(Matches)||modes.Length==0&&allocation.Count>0)issues.Add(Issue(featId,"invalid_ability_increase","Allocate the feat's ability increase to an eligible ability.","ability_increases"));
        if(feat.Name=="Resilient"&&allocation.Any(p=>Proficiencies(context,ProficiencySourceKind.Save).Contains(p.Key)))issues.Add(Issue(featId,"save_already_proficient","Choose an ability without saving throw proficiency.","ability_increases"));
        return new(issues.Count==0,issues.AsReadOnly());
    }
}
