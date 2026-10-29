using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Rules;
using Herohold.Web.Services;
namespace Herohold.TestSupport;
/// <summary>Writes the rules' typed builder choices as JSON, the shape the JSON choice readers take.</summary>
public static class NativeChoiceProjection
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static JsonObject ToJson(BuilderChoiceDefinition definition)
    {
        var details=new JsonObject();
        if(definition.Details.Minimum is {} minimum)details["minimum"]=minimum;
        if(definition.Details.Maximum is {} maximum)details["maximum"]=maximum;
        if(definition.Details.FeatOptions.Count>0)details["feat_options"]=Options(definition.Details.FeatOptions);
        return new()
        {
            ["id"]=definition.Id,["label"]=definition.Label,["section"]=definition.Section,
            ["path"]=JsonSerializer.SerializeToNode(definition.Path,Json),["choose"]=definition.Choose,["required"]=definition.Required,
            ["type"]=definition.Details.UiType??(definition.Mode switch{BuildChoiceMode.Text=>"text",BuildChoiceMode.Multi=>"multi",BuildChoiceMode.AbilityScores=>"ability_scores",BuildChoiceMode.AsiOrFeat=>"asi_or_feat",_=>"single"}),
            ["options"]=Options(definition.Options),
            ["value"]=(definition.Details.EffectiveSelection??definition.PreservedSelection) is {IsNull:false} value?JsonSerializer.SerializeToNode(value):definition.Mode==BuildChoiceMode.Multi?new JsonArray():JsonValue.Create(""),
            ["status"]=definition.Status.ToString().ToLowerInvariant(),["details"]=details
        };
    }
    private static JsonArray Options(IEnumerable<BuildChoiceOption> options)=>new([..options.Select(option=>(JsonNode?)new JsonObject
    {
        ["value"]=JsonSerializer.SerializeToNode(option.Value),["label"]=option.Label,["description"]=option.Description,["source"]=option.Source,["disabled"]=option.Disabled,["reason"]=option.Reason,
        ["children"]=new JsonArray([..option.Children.Select(child=>(JsonNode?)ToJson(child))])
    })]);
}
