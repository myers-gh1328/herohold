using Herohold.Core;
namespace Herohold.Rules;

/// <summary>Active source-owned grants and payment metadata. No description parsing, slot spending or state mutation.</summary>
public sealed class SpellGrantRules(ClassProgressionCatalog classes,RulesCatalogSnapshot catalog,RawSpeciesSpellCatalog species)
{
    private readonly IReadOnlyList<CatalogSpell> spellList=catalog.SpellRecords;
    private readonly Dictionary<string,CatalogSpell> spellLookup=catalog.SpellRecords.ToDictionary(spell=>spell.Name,StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,CatalogBackground> backgroundLookup=catalog.BackgroundRecords.ToDictionary(record=>record.Name,StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,CatalogSpecies> speciesLookup=catalog.SpeciesRecords.ToDictionary(record=>record.Name,StringComparer.OrdinalIgnoreCase);
    /// <param name="automaticSpeciesBefore">The species spells granted automatically last time (see <see cref="RawSpeciesSpellCatalog.Derive"/>).</param>
    public SpellGrantSummary Derive(BuildFacts facts,IReadOnlyDictionary<Ability,int> finalModifiers,IReadOnlyList<string>? automaticSpeciesBefore=null)
    {
        ArgumentNullException.ThrowIfNull(facts);ArgumentNullException.ThrowIfNull(finalModifiers);
        if(finalModifiers.Count!=6||finalModifiers.Any(pair=>!Enum.IsDefined(pair.Key)||pair.Value is <-100000 or >100000))throw new ArgumentException("Spell grant modifiers must contain all six bounded ability modifiers.");
        var level=facts.Level;var profile=ClassProgressionRules.AtLevel(classes,facts.ClassName,level,facts.Subclass);
        var className=profile.ClassName;var subclassName=profile.SubclassName;var classData=classes.Find(className);var classAbility=profile.Spellcasting?.Ability;
        var subclass=level>=3?classData.SubclassNamed(subclassName):null;
        var shortName=subclass?.ShortName??"";var options=facts.ClassOptions??[];
        var records=new List<SpellGrantRecord>();var keys=new HashSet<string>();var owners=new Dictionary<string,string>();var paymentBindings=new Dictionary<string,string>();var warnings=new List<string>();var granted=new List<string>();
        void GrantName(string name){if(name.Length>0&&!granted.Contains(name))granted.Add(name);}
        string RecordKey(SpellGrantRecord record)=>record.Name+"|"+SpellGrantIdentity.SerializeSource(record.Source)+"|"+record.GrantType;
        void AddRecord(SpellGrantRecord record,string owner="",string paymentResource="")
        {
            var name=record.Name;if(!spellLookup.ContainsKey(name)){warnings.Add(name+": this granted spell is absent from the selected catalog.");return;}
            var key=RecordKey(record);
            if(keys.Add(key)){records.Add(record);if(owner.Length>0)owners[key]=owner;if(paymentResource.Length>0)paymentBindings[key]=paymentResource;}
        }
        static SpellGrantRecord Record(string name,SpellGrantSource source,string ability,string type,GrantCastingRules? rules=null,bool prepared=false)
            =>new(){Name=name,Source=source,SpellcastingAbility=ability,GrantType=type,AlwaysPrepared=prepared,Rules=rules??new()};
        bool Eligible(string name,int spellLevel,string? list=null,string? schools=null,bool ritual=false)
        {
            if(!spellLookup.TryGetValue(name,out var spell)||spell.Level!=spellLevel||list is not null&&!spell.Classes.Contains(list)
                ||schools is not null&&!schools.Split(',').Contains(spell.SchoolCode)||ritual&&!spell.Ritual)return false;
            return true;
        }
        foreach(var grant in profile.SubclassGrants.SpellGrants)
        {
            var ability=grant.SpellcastingAbility.Length>0?grant.SpellcastingAbility:className=="Monk"&&shortName=="Shadow"&&grant.Name=="Minor Illusion"?"Wisdom":classAbility?.ToString()??"";
            AddRecord(Record(grant.Name,new(){Type=grant.SourceType,SubclassId=grant.SubclassId,SubclassName=grant.SubclassName,Book=grant.SourceBook},ability,grant.GrantType,prepared:grant.AlwaysPrepared));
        }
        // XPHB Shadow Arts has a fixed Focus payment, not a separate free-cast pool.
        if(className=="Monk"&&shortName=="Shadow")
            AddRecord(Record("Darkness",new(){Type="class_feature",FeatureName="Shadow Arts",ClassName="Monk"},"Wisdom","shadow_arts",new(){UsesSpellSlots=false}),"class_feature:Shadow Arts","focus");
        if(subclass?.Options.StructuredSpellChoice is {} group)
        {
            var configured=(facts.FeatureConfigurations??[]).FirstOrDefault(config=>config.FeatureId=="structured_spell_choice");
            var selected=configured?.CurrentValue??ChoiceReading.TextOf(options.Get("structured_spell_choice"));
            var chosen=group.Options.FirstOrDefault(option=>option.Name==selected||option.Id==selected);
            if(chosen is not null)
            {
                var selectedSpells=chosen.GrantedSpellsByLevel.Where(pair=>pair.Key<=level).SelectMany(pair=>pair.Value).ToList();
                if(selectedSpells.Count==0)selectedSpells.AddRange(chosen.GrantedSpells);
                foreach(var name in selectedSpells.Distinct())
                {
                    AddRecord(Record(name,new(){Type="subclass",SubclassName=subclassName,SubclassId=subclass.Id,Book=subclass.Source},classAbility?.ToString()??"","structured_subclass_spell",prepared:true));
                }
            }
        }
        void ClassOption(string key,string feature,int minimum,string type,Func<string,bool> eligible,GrantCastingRules? rule=null)
        {
            if(level<minimum)return;
            foreach(var name in ChoiceReading.Names(options.Get(key)))
            {
                if(!eligible(name)){warnings.Add(feature+": choose an eligible spell before casting.");continue;}
                AddRecord(Record(name,new(){Type="class_option",SourceName=feature,ClassName=className,LevelGained=minimum},classAbility?.ToString()??"",type,rule));
            }
        }
        var style=ChoiceReading.TextOf(options.Get("fighting_style"));
        if(className=="Paladin"&&style=="Blessed Warrior")ClassOption("blessed_warrior_cantrips","Blessed Warrior",2,"fighting_style_cantrip",name=>Eligible(name,0,"Cleric"));
        if(className=="Ranger"&&style=="Druidic Warrior")ClassOption("druidic_warrior_cantrips","Druidic Warrior",2,"fighting_style_cantrip",name=>Eligible(name,0,"Druid"));
        if(className=="Bard"&&shortName=="Lore")ClassOption("magical_discoveries","Magical Discoveries",6,"magical_discoveries",name=>spellLookup.TryGetValue(name,out var spell)&&(spell.Level==0||profile.Spellcasting?.AvailableSpellLevels.Contains(spell.Level)==true)&&spell.Classes.Intersect(new[]{"Cleric","Druid","Wizard"}).Any());
        if(className=="Wizard")
        {
            var book=(facts.Spells?.Spellbook??[]).Where(n=>n.Length>0).Concat(options.Where(pair=>pair.Key=="savant_spells"||pair.Key.StartsWith("savant_spells_level_")).SelectMany(pair=>ChoiceReading.Names(pair.Value))).ToHashSet();
            foreach(var spellLevel in new[]{1,2})ClassOption("spell_mastery_level_"+spellLevel,"Spell Mastery",18,"spell_mastery",name=>Eligible(name,spellLevel,"Wizard")&&book.Contains(name)&&spellLookup[name].CastingTime=="1 action",new(){AtWillLowestLevel=true,CanUseSpellSlots=true});
            ClassOption("signature_spells","Signature Spells",20,"signature_spells",name=>Eligible(name,3,"Wizard")&&book.Contains(name),new(){FreeCasts=1,Recharge="short_or_long_rest",CanUseSpellSlots=true});
        }
        if(className=="Warlock")
        {
            // Reviewed XPHB optionalfeatures records: finite declarations, never interpreted description text.
            var invocationSpells=new (string Invocation,string Spell,int Minimum,bool AtWill,string Restriction)[]{
                ("Armor of Shadows","Mage Armor",1,true,"Self only."),("Ascendant Step","Levitate",5,true,"Self only."),
                ("Fiendish Vigor","False Life",2,true,"Self only; use the maximum temporary-HP die result. Resolve temporary HP manually."),
                ("Mask of Many Faces","Disguise Self",2,true,""),("Master of Myriad Forms","Alter Self",5,true,""),
                ("Misty Visions","Silent Image",2,true,""),("One with Shadows","Invisibility",5,true,"Self only, while in Dim Light or Darkness; verify this condition manually."),
                ("Otherworldly Leap","Jump",2,true,"Self only."),("Visions of Distant Realms","Arcane Eye",9,true,""),
                ("Whispers of the Grave","Speak with Dead",7,true,""),("Pact of the Chain","Find Familiar",1,true,"Cast as a Magic action; choose the familiar form manually."),
                ("Gift of the Depths","Water Breathing",5,false,"")};
            var invocations=ChoiceReading.Names(options.Get("eldritch_invocations")).ToHashSet(StringComparer.Ordinal);
            foreach(var entry in invocationSpells.Where(entry=>level>=entry.Minimum&&invocations.Contains(entry.Invocation)))
            {
                var rules=entry.AtWill?new GrantCastingRules{AtWillLowestLevel=true,CanUseSpellSlots=false}:new GrantCastingRules{CanUseSpellSlots=false,FreeCasts=1,Recharge="long_rest"};
                if(entry.Restriction.Length>0)warnings.Add(entry.Invocation+": "+entry.Restriction);
                AddRecord(Record(entry.Spell,new(){Type="invocation",SourceName=entry.Invocation,ClassName="Warlock"},"Charisma","invocation_spell",rules));
            }
            if(invocations.Contains("Pact of the Tome"))
            {
                var otherPrepared=(facts.Spells?.Prepared??[]).Concat(facts.Spells?.Cantrips??[]).Where(n=>n.Length>0)
                    .Concat(records.Where(record=>record.AlwaysPrepared).Select(record=>record.Name)).ToHashSet(StringComparer.Ordinal);
                foreach(var (key,spellLevel) in new[]{("pact_tome_cantrips",0),("pact_tome_rituals",1)})
                    foreach(var name in ChoiceReading.Names(options.Get(key)).Distinct(StringComparer.Ordinal))
                    {
                        if(!Eligible(name,spellLevel,ritual:spellLevel==1)||otherPrepared.Contains(name)){warnings.Add("Pact of the Tome: choose eligible spells you do not already have prepared from another source.");continue;}
                        AddRecord(Record(name,new(){Type="invocation",SourceName="Pact of the Tome",ClassName="Warlock"},"Charisma",spellLevel==0?"pact_tome_cantrip":"pact_tome_ritual",new(){CanUseSpellSlots=spellLevel>0,AtWillLowestLevel=spellLevel==0},true));
                    }
            }
            foreach(var (sourceLevel,spellLevel) in new[]{(11,6),(13,7),(15,8),(17,9)})
            {
                if(level<sourceLevel)continue;
                foreach(var name in ChoiceReading.Names(options.Get("mystic_arcanum_"+spellLevel)))
                {
                    if(!Eligible(name,spellLevel,"Warlock")){warnings.Add("Mystic Arcanum: choose an eligible level-specific Warlock spell.");continue;}
                    var source=new SpellGrantSource{Type="class_feature",ClassName="Warlock",FeatureName="Mystic Arcanum",SourceName="Mystic Arcanum",Level=sourceLevel};
                    AddRecord(Record(name,source,"Charisma","mystic_arcanum",new(){FreeCasts=1,Recharge="long_rest",UsesPactSlots=false},true));
                }
            }
        }
        foreach(var feat in facts.Feats??[])
        {
            var featName=feat.Name;var choices=feat.Choices;var ability=ChoiceReading.TextOf(choices.Get("ability"));
            var source=new SpellGrantSource{Type="feat",InstanceId=feat.InstanceId,FeatId=feat.FeatId,SourceName=featName};
            var free=new GrantCastingRules{FreeCasts=1,Recharge="long_rest",CanUseSpellSlots=true};
            if(featName=="Telekinetic")AddRecord(Record("Mage Hand",source,ability,"feat_cantrip"));
            if(featName=="Telepathic")AddRecord(Record("Detect Thoughts",source,ability,"feat_spell",free,true));
            if(featName is "Fey-Touched" or "Shadow-Touched")
            {
                AddRecord(Record(featName=="Fey-Touched"?"Misty Step":"Invisibility",source,ability,"feat_spell",free,true));
                var chosen=ChoiceReading.TextOf(choices.Get("spell"));
                if(chosen.Length>0)
                {if(Eligible(chosen,1,schools:featName=="Fey-Touched"?"D,E":"I,N"))AddRecord(Record(chosen,source,ability,"feat_spell",free,true));else warnings.Add(featName+": its selected spell does not match the feat's structured level/school rule.");}
            }
            if(featName=="Ritual Caster")
                foreach(var name in ChoiceReading.Names(choices.Get("spells")))
                    if(Eligible(name,1,ritual:true))AddRecord(Record(name,source,ability,"feat_ritual",new(){RitualOnly=true}));else warnings.Add("Ritual Caster: choose eligible level1 ritual spells.");
        }
        void Feature(string name,string feature,int minimum,Ability ability,GrantCastingRules rules,bool subclassFeature=false)
        {
            if(level<minimum)return;
            var source=new SpellGrantSource{Type=subclassFeature?"subclass_feature":"class_feature",ClassName=className,FeatureName=feature,SourceName=feature,Level=minimum,
                SubclassId=subclassFeature?subclass?.Id??"":null,SubclassName=subclassFeature?subclassName:null};
            AddRecord(Record(name,source,ability.ToString(),"feature_spell_grant",rules,true));
        }
        if(className=="Bard")foreach(var name in new[]{"Power Word Heal","Power Word Kill"})Feature(name,"Words of Creation",20,Ability.Charisma,new());
        if(className=="Warlock")Feature("Contact Other Plane","Contact Patron",9,Ability.Charisma,new(){FreeCasts=1,Recharge="long_rest",UsesSpellSlots=false});
        if(className=="Barbarian"&&shortName=="Wild Heart")Feature("Commune with Nature","Nature Speaker",10,Ability.Wisdom,new(){RitualOnly=true,UsesSpellSlots=false},true);
        if(className=="Ranger"&&shortName=="Fey Wanderer")Feature("Misty Step","Misty Wanderer",15,Ability.Wisdom,new(){FreeCasts=Math.Max(1,finalModifiers[Ability.Wisdom]),Recharge="long_rest",UsesSpellSlots=false},true);
        if(className=="Fighter"&&shortName=="Psi Warrior")Feature("Telekinesis","Telekinetic Master",18,Ability.Intelligence,new(){FreeCasts=1,Recharge="long_rest",UsesSpellSlots=false},true);
        if(className=="Sorcerer"&&shortName=="Draconic")Feature("Summon Dragon","Dragon Companion",18,Ability.Charisma,new(){FreeCasts=1,Recharge="long_rest",UsesSpellSlots=true},true);
        backgroundLookup.TryGetValue(facts.Background,out var background);
        speciesLookup.TryGetValue(facts.Species,out var kind);
        var origins=NativeOriginFeatInstances.Derive(facts,background,kind).Where(instance=>instance.Name.StartsWith("Magic Initiate",StringComparison.Ordinal));
        foreach(var origin in origins)
        {
            var choices=origin.Choices;
            var list=new[]{"Cleric","Druid","Wizard"}.FirstOrDefault(origin.Name.Contains)??"";var source=new SpellGrantSource{Type="origin_feat",SourceName=origin.Name};
            if(origin.GeneralFeat)
            {
                var record=(facts.Feats??[])[int.Parse(origin.ChoicePath[1],System.Globalization.CultureInfo.InvariantCulture)];source=new(){Type="feat",InstanceId=origin.SourceId[5..],FeatId=record.FeatId,SourceName=origin.Name};
            }
            foreach(var spell in spellList)
            {
                var name=spell.Name;var spellLevel=spell.Level;
                if(!spell.Classes.Contains(list)||!(spellLevel==0&&ChoiceReading.Names(choices.Get("cantrips")).Contains(name)||spellLevel==1&&ChoiceReading.TextOf(choices.Get("spell"))==name))continue;
                var rules=spellLevel==0?new GrantCastingRules{AtWillLowestLevel=true,CanUseSpellSlots=false}:new GrantCastingRules{CanUseSpellSlots=true,FreeCasts=1,Recharge="long_rest"};
                AddRecord(Record(name,source,ChoiceReading.TextOf(choices.Get("spellcasting_ability")),spellLevel==0?"origin_feat_cantrip":"origin_feat_spell",rules,spellLevel==1),origin.SourceId);
            }
        }
        var metadata=new List<SpellGrantMetadata>();var identities=new List<SpellGrantSourceIdentity>();
        foreach(var record in records)
        {
            var source=record.Source;var label=source.Label;
            var abilityText=record.SpellcastingAbility;
            if(!Enum.TryParse<Ability>(abilityText,out var ability)||!Enum.IsDefined(ability)){warnings.Add(label+": choose its spellcasting ability before casting.");continue;}
            var rules=record.Rules;var name=record.Name;var spellLevel=spellLookup[name].Level;var ids=SpellGrantIdentity.ForSource(source,name);var maximum=Math.Max(0,rules.FreeCasts);
            if(maximum>100000)throw new ArgumentException("A spell grant capacity exceeds its supported bounds.");
            var grant=new SpellGrantMetadata{Id=ids.GrantId,SpellName=name,Ability=ability,ResourceId=paymentBindings.GetValueOrDefault(RecordKey(record))??(maximum>0?ids.ResourceId:null),MaxUses=maximum,Recovery=rules.Recharge,AllowsSlots=rules.AllowsSlots&&spellLevel>0,BaseLevel=spellLevel,Source=label,AtWill=rules.AtWillLowestLevel||spellLevel==0,RitualOnly=rules.RitualOnly};
            Put(metadata,grant);GrantName(name);
            var sourceKind=source.Type;var owner=owners.GetValueOrDefault(RecordKey(record))??(sourceKind=="feat"?"feat:"+(source.InstanceId??source.FeatId??label):sourceKind=="origin_feat"?"origin:"+label:sourceKind=="class_option"?"class_options:"+OptionKey(label,spellLevel):sourceKind+":"+label);
            identities.Add(new(grant.Id,owner,sourceKind,label,ability));
        }
        var speciesGrants=species.Derive(facts,automaticSpeciesBefore??[]);warnings.AddRange(speciesGrants.Warnings);
        foreach(var grant in speciesGrants.Metadata){Put(metadata,grant);identities.Add(new(grant.Id,"species:"+grant.Source,"species",grant.Source,grant.Ability));GrantName(grant.SpellName);}
        if(className=="Ranger"&&level>=1&&spellLookup.TryGetValue("Hunter's Mark",out var mark))
        {
            var maximum=profile.Resources.Count("favored_enemy");
            if(maximum>0)
            {var ids=SpellGrantIdentity.ForLabel("Favored Enemy","Hunter's Mark");Put(metadata,new(){Id=ids.GrantId,SpellName="Hunter's Mark",Ability=Ability.Wisdom,ResourceId="hunters",MaxUses=maximum,Recovery="long_rest",AllowsSlots=true,BaseLevel=mark.Level,Source="Favored Enemy"});identities.Add(new(ids.GrantId,"class_feature:Favored Enemy","class_feature","Favored Enemy",Ability.Wisdom));GrantName("Hunter's Mark");}
        }
        if(className=="Cleric"&&ChoiceReading.TextOf(options.Get("divine_order"))=="Thaumaturge"||className=="Druid"&&ChoiceReading.TextOf(options.Get("primal_order"))=="Magician")foreach(var name in ChoiceReading.Names(options.Get("extra_cantrips")))if(Eligible(name,0,className))GrantName(name);
        return new(){Records=records.AsReadOnly(),Metadata=metadata.AsReadOnly(),Sources=identities.Distinct().ToList().AsReadOnly(),GrantedSpellNames=granted.AsReadOnly(),SpeciesSpells=speciesGrants.SelectedSpells,AutomaticSpeciesSpells=speciesGrants.AutomaticSpells,Warnings=warnings.Distinct().ToList().AsReadOnly()};
    }
    private static string OptionKey(string label,int spellLevel)=>label switch{"Blessed Warrior"=>"blessed_warrior_cantrips","Druidic Warrior"=>"druidic_warrior_cantrips","Magical Discoveries"=>"magical_discoveries","Spell Mastery"=>"spell_mastery_level_"+spellLevel,"Signature Spells"=>"signature_spells",_=>label};
    private static void Put(List<SpellGrantMetadata> records,SpellGrantMetadata record){var index=records.FindIndex(item=>item.Id==record.Id);if(index<0)records.Add(record);else records[index]=record;}
}
