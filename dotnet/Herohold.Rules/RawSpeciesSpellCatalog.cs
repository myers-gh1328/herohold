using Herohold.Core;
namespace Herohold.Rules;
public sealed class RawSpeciesSpellCatalog
{
    private readonly SpeciesSourceCatalog races;
    private readonly Dictionary<string,CatalogSpell> spellRecords;
    private RawSpeciesSpellCatalog(SpeciesSourceCatalog races,Dictionary<string,CatalogSpell> spells){this.races=races;spellRecords=spells;}
    public static RawSpeciesSpellCatalog Parse(string rawRacesJson,RulesCatalogSnapshot catalog)=>From(SpeciesSourceCatalog.Parse(rawRacesJson),catalog);
    public static RawSpeciesSpellCatalog From(SpeciesSourceCatalog races,RulesCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(races);ArgumentNullException.ThrowIfNull(catalog);
        return new(races,catalog.SpellRecords.ToDictionary(record=>record.Name,StringComparer.OrdinalIgnoreCase));
    }
    /// <param name="automaticBefore">The species spells the rules granted automatically last time; they are not the player's selections.</param>
    public SpeciesSpellGrantSummary Derive(BuildFacts facts,IReadOnlyList<string> automaticBefore)
    {
        ArgumentNullException.ThrowIfNull(facts);var level=facts.Level;
        if(level is <1 or >20)throw new ArgumentException("Species spell grants require a level1–20 context.");
        var name=facts.Species;var options=facts.SpeciesOptions??[];
        var selectedLineage=ChoiceReading.TextOf(options.Get("lineage"));
        if(selectedLineage.Length==0)selectedLineage=options.FirstOrDefault(pair=>pair.Key.EndsWith("_lineage",StringComparison.Ordinal)&&pair.Value.Text is not null).Value?.Text??"";
        var label=name+(selectedLineage.Length>0?" · "+selectedLineage:"");
        var selected=ChoiceReading.Names(options.Get("lineage_spells")).Except(automaticBefore,StringComparer.Ordinal).ToList();
        var automatic=new List<string>();var metadata=new List<SpellGrantMetadata>();var warnings=new List<string>();
        if(races.Find(name) is not {} race)return new(){SelectedSpells=selected.AsReadOnly()};
        var variant=race.Version(selectedLineage);
        var additions=(variant?.AdditionalSpells??race.AdditionalSpells??[]).Where(record=>record.Name.Length==0||record.Name==selectedLineage).ToList();
        string? Resolve(string reference)
        {var spell=reference.Split('|')[0].Split('#')[0];return spellRecords.TryGetValue(spell,out var record)?record.Name:null;}
        bool Gate(string gate)=>gate=="_"||int.TryParse(gate,out var minimum)&&minimum<=level;
        IEnumerable<string> References(IReadOnlyList<SpeciesSpellReference> references,bool includeChoices)
        {
            foreach(var reference in references)
            {
                if(reference.Choose is null){if(Resolve(reference.Spell) is {} spell)yield return spell;continue;}
                if(includeChoices&&reference.Choose=="level=0|class=Wizard")
                    foreach(var candidate in ChoiceReading.Names(options.Get("lineage_spells")))
                        if(spellRecords.TryGetValue(candidate,out var spell)&&spell.Level==0&&spell.Classes.Contains("Wizard"))yield return spell.Name;
            }
        }
        void Add(string spellName,Ability? ability,int maximum=0,bool atWill=false,bool slots=false)
        {
            if(!spellRecords.TryGetValue(spellName,out var spell))return;
            if(ability is null){warnings.Add(label+": choose its spellcasting ability before casting.");return;}
            var canonicalName=spell.Name;var spellLevel=spell.Level;var ids=SpellGrantIdentity.ForLabel(label,canonicalName);
            var grant=new SpellGrantMetadata{Id=ids.GrantId,SpellName=canonicalName,Ability=ability.Value,ResourceId=maximum>0?ids.ResourceId:null,MaxUses=maximum,Recovery=maximum>0?"long_rest":"none",AllowsSlots=slots&&spellLevel>0,BaseLevel=spellLevel,Source=label,AtWill=atWill||spellLevel==0};
            var index=metadata.FindIndex(record=>record.Id==grant.Id);if(index<0)metadata.Add(grant);else metadata[index]=grant;
        }
        foreach(var addition in additions)
        {
            foreach(var gates in new[]{addition.Known,addition.Innate})
                foreach(var gate in gates.Where(g=>Gate(g.Gate)))
                    foreach(var spell in References(gate.Spells,false))if(!automatic.Contains(spell))automatic.Add(spell);
            var selectedAbility=addition.AbilityChoices is not null?ChoiceReading.TextOf(options.Get("lineage_spellcasting_ability")):FullAbility(addition.FixedAbility);
            Ability? ability=Enum.TryParse<Ability>(selectedAbility,out var parsed)&&Enum.IsDefined(parsed)?parsed:null;
            foreach(var gate in addition.Known.Where(g=>Gate(g.Gate)))foreach(var spell in References(gate.Spells,true))Add(spell,ability,atWill:true);
            foreach(var gate in addition.Innate.Where(g=>Gate(g.Gate)))
                foreach(var daily in gate.Daily)
                {
                    var maximum=daily.Key=="pb"?CharacterCalculations.ProficiencyBonus(level):int.TryParse(daily.Key.TrimEnd('e'),out var count)?count:0;
                    if(maximum is <1 or >100000){warnings.Add(label+": the free-cast recovery needs manual review.");continue;}
                    // XPHB Elf/Tiefling lineage and Forest Gnome innate entries have an explicit Long Rest policy.
                    if(name is not ("Elf" or "Tiefling" or "Gnome")){warnings.Add(label+": this species spell recharge is not modeled.");continue;}
                    foreach(var spell in References(daily.Value,true))Add(spell,ability,maximum,slots:true);
                }
        }
        if(name=="Gnome"&&selectedLineage=="Forest Gnome")
        {
            if(!automatic.Contains("Speak with Animals"))automatic.Add("Speak with Animals");
            Ability? ability=Enum.TryParse<Ability>(ChoiceReading.TextOf(options.Get("lineage_spellcasting_ability")),out var parsed)&&Enum.IsDefined(parsed)?parsed:null;
            Add("Speak with Animals",ability,CharacterCalculations.ProficiencyBonus(level),slots:true);
        }
        return new(){SelectedSpells=selected.Concat(automatic).Distinct(StringComparer.Ordinal).ToList().AsReadOnly(),AutomaticSpells=automatic.AsReadOnly(),Metadata=metadata.AsReadOnly(),Warnings=warnings.Distinct().ToList().AsReadOnly()};
    }
    private static string FullAbility(string value)=>value switch{"str"=>"Strength","dex"=>"Dexterity","con"=>"Constitution","int"=>"Intelligence","wis"=>"Wisdom","cha"=>"Charisma",_=>value};
}
