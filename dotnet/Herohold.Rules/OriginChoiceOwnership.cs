using Herohold.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
/// <summary>Rebinds legacy origin choice locations without transferring choices between permanent owners.</summary>
public static class OriginChoiceOwnership
{
    /// <param name="before">The build before the edit.</param>
    /// <param name="after">The edited build, as a choice tree.</param>
    /// <param name="patch">The edit itself, as a choice tree; its explicit origin choices win.</param>
    public static ChoiceValue Rebind(BuildFacts before,ChoiceValue after,ChoiceValue patch,RulesCatalogSnapshot catalog)
    {
        var result=after;
        IReadOnlyList<NativeOriginFeatInstance> Instances(BuildFacts c)=>NativeOriginFeatInstances.Derive(c,catalog.Background(c.Background),catalog.SpeciesNamed(c.Species)).Where(instance=>!instance.GeneralFeat).ToArray();
        var oldInstances=Instances(before);var nextInstances=Instances(BuildFacts.FromChoiceTree(result));
        var owners=result.At(["folio_origin_choice_owners"]);
        if(owners is {IsNull:false,Fields:null})throw new ArgumentException("Origin choice ownership must be an object.");
        var cache=new Dictionary<string,ChoiceValue>(owners?.Fields??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal);
        if(cache.Count>50||cache.Any(pair=>string.IsNullOrWhiteSpace(pair.Key)||pair.Key.Length>200||pair.Value.Fields is not {} value||value.Count>20||JsonSerializer.Serialize(pair.Value).Length>32768))throw new ArgumentException("Origin choice ownership exceeds its supported bounds.");
        var changed=false;
        foreach(var magic in new[]{true,false})
        {
            bool Matches(NativeOriginFeatInstance instance)=>magic?instance.Name.StartsWith("Magic Initiate",StringComparison.Ordinal):instance.Name is "Skilled" or "Crafter" or "Musician";
            var previous=oldInstances.Where(Matches).ToArray();var next=nextInstances.Where(Matches).ToArray();
            string Signature(IEnumerable<NativeOriginFeatInstance> instances)=>string.Join("|",instances.Select(instance=>instance.SourceId+"="+string.Join("/",instance.ChoicePath)));
            if(Signature(previous)==Signature(next))continue;
            changed=true;string[] fields=magic?["cantrips","spell","spellcasting_ability"]:["skills","tools","proficiencies"];
            Dictionary<string,ChoiceValue> Selected(IReadOnlyDictionary<string,ChoiceValue> choices)=>choices.Where(pair=>fields.Contains(pair.Key,StringComparer.Ordinal)).ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
            foreach(var instance in previous)cache[instance.SourceId]=ChoiceValue.Record(Selected(instance.Choices));
            var legacy=result.At(["origin_feat_choices"])?.Fields??new Dictionary<string,ChoiceValue>();
            result=result.With(["origin_feat_choices"],ChoiceValue.Record(legacy.Where(pair=>!fields.Contains(pair.Key,StringComparer.Ordinal))));
            foreach(var instance in next)
            {
                var old=previous.FirstOrDefault(old=>old.SourceId==instance.SourceId);
                var selected=old is not null?Selected(old.Choices):cache.GetValueOrDefault(instance.SourceId)?.Fields is {} saved?new Dictionary<string,ChoiceValue>(saved,StringComparer.Ordinal):instance.ChoicePath.Count==2?Selected(instance.Choices):[];
                if(patch.At(instance.ChoicePath)?.Fields is {} explicitChoices)foreach(var pair in explicitChoices.Where(pair=>fields.Contains(pair.Key,StringComparer.Ordinal)))selected[pair.Key]=pair.Value;
                var destination=result.At(instance.ChoicePath)?.Fields??new Dictionary<string,ChoiceValue>();
                result=result.With(instance.ChoicePath,ChoiceValue.Record(destination.Where(pair=>!fields.Contains(pair.Key,StringComparer.Ordinal)).Concat(selected)));
            }
        }
        if(changed){if(cache.Count>50)throw new ArgumentException("The origin choice ownership history is full.");result=result.With(["folio_origin_choice_owners"],ChoiceValue.Record(cache));}
        return result;
    }
    private static string Text(JsonNode? node)=>node is JsonValue value&&value.TryGetValue<string>(out var text)?text:"";
}
