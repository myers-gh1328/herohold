using Herohold.Core;
namespace Herohold.Rules;
/// <summary>Applies a player's edit of build choices, keeping dependent choices consistent.</summary>
public sealed class CharacterEditRules(RulesCatalogSnapshot catalog)
{
    private static readonly HashSet<string> Allowed=["name","species","background","class","subclass","base_ability_scores","ability_score_method","class_options","species_options","background_options","origin_feat_choices","folio_origin_feat_choices",
        "origin_languages","spells","weapon_masteries","presentation_preferences","feature_configurations"];
    /// <summary>Editable draft facts that are not part of the build; the workflow applies them to the draft itself.</summary>
    public static IReadOnlySet<string> DraftFacts {get;}=new HashSet<string>(["notes","include_default_equipment","custom_equipment","equipment_modifier_bonuses"],StringComparer.Ordinal);
    /// <param name="patch">Named parts to set, addressed by their names in the build; named parts merge and other values replace.</param>
    public BuildFacts Apply(BuildFacts character,ChoiceValue patch)
    {
        ArgumentNullException.ThrowIfNull(character);ArgumentNullException.ThrowIfNull(patch);
        var changes=patch.Fields??throw new ArgumentException("Only supported character choices may be edited.");
        if(changes.Keys.Any(key=>!Allowed.Contains(key)))throw new ArgumentException("Only supported character choices may be edited.");
        if(changes.TryGetValue("origin_languages",out var languages)&&(languages.Items is not {} names||names.Count>100||names.Any(name=>name.Text is not {} text||!LanguageRules.StandardLanguages.Contains(text,StringComparer.Ordinal))))
            throw new ArgumentException("Origin language choices must be a bounded list of standard languages; imported languages stay in their separate ownership record.");
        foreach(var key in new[]{"class_options","species_options","background_options","origin_feat_choices","folio_origin_feat_choices","base_ability_scores","spells","presentation_preferences"})if(changes.TryGetValue(key,out var value)&&value.Fields is null)throw new ArgumentException(key+" must be an object.");
        foreach(var key in new[]{"name","species","background","class","subclass","ability_score_method"})if(changes.TryGetValue(key,out var value)&&value.Text is null)throw new ArgumentException(key+" must be text.");
        var updated=character.AsChoiceTree();
        foreach(var pair in new[]{("class","class_options"),("species","species_options"),("background","background_options")})
        {
            if(!changes.TryGetValue(pair.Item1,out var chosen)||chosen.Text==(updated.At([pair.Item1])?.Text??""))continue;
            if(pair.Item1=="class"&&character.Level!=1)throw new ArgumentException("Changing class after level 1 requires an explicit progression workflow.");
            updated=updated.With([pair.Item2],ChoiceValue.Record([]));
            if(pair.Item1=="class")
            {
                var none=ChoiceValue.ListOf([]);
                updated=updated.With(["subclass"],ChoiceValue.Of("")).With(["spells"],ChoiceValue.Record([new("cantrips",none),new("prepared",none),new("known",none),new("spellbook",none)])).With(["weapon_masteries"],none)
                    .With(["class_levels"],ChoiceValue.Record([new(chosen.Text!,ChoiceValue.Of(1))])).With(["hp_history"],none).With(["hp_rolls"],none).With(["level_history"],none);
            }
        }
        var merged=Merge(updated,patch);
        if(changes.GetValueOrDefault("class_options")?.Fields is {} changedOptions&&merged.At(["feature_configurations"])?.Items is {} configurations)
            merged=merged.With(["feature_configurations"],ChoiceValue.ListOf(configurations.Select(configuration=>
                configuration.At(["feature_id"])?.Text is {} key&&changedOptions.GetValueOrDefault(key)?.Text is {} selected?configuration.With(["current_value"],ChoiceValue.Of(selected)):configuration)));
        merged=OriginChoiceOwnership.Rebind(character,merged,patch,catalog);
        ChoiceValue Split(ChoiceValue values)
        {
            if(values.Fields is not {} fields||!fields.TryGetValue("proficiencies",out var proficiencies))return values;
            if(proficiencies.Items is not {} selected||selected.Any(n=>n.Text is not {} text||!text.StartsWith("Skill: ",StringComparison.Ordinal)&&!text.StartsWith("Tool: ",StringComparison.Ordinal)))throw new ArgumentException("Choose supported Skilled proficiencies.");
            var names=selected.Select(n=>n.Text!).ToArray();
            return ChoiceValue.Record(fields.Where(p=>p.Key is not ("proficiencies" or "skills" or "tools")).Concat([new("skills",ChoiceValue.ListOf(names.Where(s=>s.StartsWith("Skill: ",StringComparison.Ordinal)).Select(s=>ChoiceValue.Of(s[7..])))),new("tools",ChoiceValue.ListOf(names.Where(s=>s.StartsWith("Tool: ",StringComparison.Ordinal)).Select(s=>ChoiceValue.Of(s[6..]))))]));
        }
        if(merged.At(["origin_feat_choices"]) is {Fields:not null} origin)merged=merged.With(["origin_feat_choices"],Split(origin));
        if(merged.At(["folio_origin_feat_choices"])?.Fields is {} later)merged=merged.With(["folio_origin_feat_choices"],ChoiceValue.Record(later.Select(p=>KeyValuePair.Create(p.Key,p.Value.Fields is not null?Split(p.Value):p.Value))));
        return BuildFacts.FromChoiceTree(merged);
    }
    /// <summary>Named parts merge; any other value replaces.</summary>
    private static ChoiceValue Merge(ChoiceValue defaults,ChoiceValue saved)
    {
        if(defaults.Fields is not {} left||saved.Fields is not {} right)return saved;
        var merged=new Dictionary<string,ChoiceValue>(left,StringComparer.Ordinal);
        foreach(var pair in right)merged[pair.Key]=merged.TryGetValue(pair.Key,out var existing)&&existing.Fields is not null&&pair.Value.Fields is not null?Merge(existing,pair.Value):pair.Value;
        return new(){Fields=merged};
    }
}
