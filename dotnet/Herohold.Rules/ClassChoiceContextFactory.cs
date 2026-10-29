using Herohold.Core;
namespace Herohold.Rules;
public static class ClassChoiceContextFactory
{
    /// <param name="grantDetails">Granted spell records a calculation found, which give some cantrips and prepared spells their owner.</param>
    /// <param name="shownExpertise">The Expertise the character showed, for legacy Rogue choices.</param>
    public static ClassChoiceContext FromBuild(BuildFacts facts,NativeBuildSummary build,IReadOnlyList<SpellGrantRecord> grantDetails,IReadOnlyList<string> shownExpertise)
    {
        ArgumentNullException.ThrowIfNull(facts);ArgumentNullException.ThrowIfNull(build);
        var options=new Dictionary<string,ChoiceValue>(facts.ClassOptions??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal);
        var configurations=new Dictionary<string,string>();
        foreach(var configuration in facts.FeatureConfigurations??[])
        {var key=configuration.FeatureId;var value=configuration.CurrentValue;if(key.Length>0&&value.Length>0)configurations.TryAdd(key,value);}
        var cantrips=new List<ClassChoiceSpellSource>();
        var prepared=new List<ClassChoiceOwnedSpell>();
        foreach(var name in (facts.Spells?.Cantrips??[]).Where(n=>n.Length>0))cantrips.Add(new(name,"class:"+build.ClassName,[build.ClassName]));
        foreach(var name in (facts.Spells?.Prepared??[]).Where(n=>n.Length>0))prepared.Add(new(name,"class:"+build.ClassName));
        var invocations=ChoiceReading.Names(options.GetValueOrDefault("eldritch_invocations")).ToHashSet(StringComparer.Ordinal);
        foreach(var detail in grantDetails)
        {
            var source=detail.Source;var name=detail.Name;if(name.Length==0)continue;
            var sourceName=source.SourceName??source.FeatureName??"";
            // Only explicit Pact of the Tome source identity grants its cantrips Warlock ownership.
            var counts=build.ClassName=="Warlock"&&invocations.Contains("Pact of the Tome")&&source.Type=="invocation"&&sourceName=="Pact of the Tome"?new[]{"Warlock"}:Array.Empty<string>();
            var tome=source.Type=="invocation"&&sourceName=="Pact of the Tome";
            if(tome&&counts.Length==0)continue;
            var owner=tome?"class_options:"+(detail.GrantType=="pact_tome_ritual"?"pact_tome_rituals":"pact_tome_cantrips"):"grant:"+(source.InstanceId??sourceName);
            cantrips.Add(new(name,owner,counts));
            if(detail.AlwaysPrepared)prepared.Add(new(name,owner));
        }
        if(build.ClassName=="Warlock"&&invocations.Contains("Pact of the Tome"))
        {
            foreach(var name in ChoiceReading.Names(options.GetValueOrDefault("pact_tome_cantrips")))cantrips.Add(new(name,"class_options:pact_tome_cantrips",["Warlock"]));
            foreach(var name in ChoiceReading.Names(options.GetValueOrDefault("pact_tome_rituals")))prepared.Add(new(name,"class_options:pact_tome_rituals"));
        }
        foreach(var key in new[]{"extra_cantrips","blessed_warrior_cantrips","druidic_warrior_cantrips"})
        {
            var level=facts.Level;
            var active=key switch
            {
                "extra_cantrips"=>build.ClassName=="Cleric"&&ChoiceReading.TextOf(options.GetValueOrDefault("divine_order"))=="Thaumaturge"||build.ClassName=="Druid"&&ChoiceReading.TextOf(options.GetValueOrDefault("primal_order"))=="Magician",
                "blessed_warrior_cantrips"=>build.ClassName=="Paladin"&&level>=2&&ChoiceReading.TextOf(options.GetValueOrDefault("fighting_style"))=="Blessed Warrior",
                _=>build.ClassName=="Ranger"&&level>=2&&ChoiceReading.TextOf(options.GetValueOrDefault("fighting_style"))=="Druidic Warrior"
            };
            if(!active)continue;
            var counts=new[]{build.ClassName};
            foreach(var name in ChoiceReading.Names(options.GetValueOrDefault(key)))cantrips.Add(new(name,"class_options:"+key,counts));
        }
        var savant=new List<ClassChoiceOwnedSpell>();
        foreach(var pair in options.Where(pair=>pair.Key=="savant_spells"||pair.Key.StartsWith("savant_spells_level_",StringComparison.Ordinal)))
            foreach(var name in ChoiceReading.Names(pair.Value))savant.Add(new(name,"class_options:"+pair.Key));
        return new(){ClassName=build.ClassName,SubclassName=build.SubclassName,Level=facts.Level,
            ClassOptions=options,FeatureConfigurations=configurations,ProficiencySources=build.ProficiencySources.Select(source=>source with{}).ToList().AsReadOnly(),
            CantripSources=cantrips.AsReadOnly(),PreparedSpellSources=prepared.AsReadOnly(),Spellbook=(facts.Spells?.Spellbook??[]).Where(n=>n.Length>0).ToList().AsReadOnly(),SavantSpells=savant.AsReadOnly(),LegacyRogueExpertise=shownExpertise.ToList().AsReadOnly()};
    }
}
