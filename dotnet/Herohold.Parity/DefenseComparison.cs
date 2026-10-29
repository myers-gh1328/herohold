using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Rules;

internal static class DefenseComparison
{
    public static async Task<int> RunAsync(string path)
    {
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        try
        {
            var document=JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            if(document["schemaVersion"]?.GetValue<int>()!=1)throw new InvalidDataException("Unsupported defense fixtures.");
            var output=new JsonObject{["armor"]=new JsonArray(),["movement"]=new JsonArray()};
            foreach(var section in new[]{"armor","movement"})
            foreach(var fixture in document[section]!.AsArray())
            {
                JsonNode result;
                if(section=="armor")
                {
                    var input=fixture!["input"]!.Deserialize<ArmorInput>(options)!;
                    var before=JsonSerializer.Serialize(input,options);
                    var armor=ArmorRules.Calculate(input);
                    if(before!=JsonSerializer.Serialize(input,options)||armor.Best.Calculation.Total!=armor.Best.Calculation.Terms.Sum(t=>t.Value))
                        throw new InvalidDataException("Armor purity or calculation terms failed.");
                    result=JsonValue.Create(armor.Best.Calculation.Total)!;
                }
                else
                {
                    var input=fixture!["input"]!.Deserialize<MovementInput>(options)!;
                    var before=JsonSerializer.Serialize(input,options);
                    var movement=MovementRules.Calculate(input);
                    if(before!=JsonSerializer.Serialize(input,options)||movement.Speed!=movement.Calculation.Terms.Sum(t=>t.Value))
                        throw new InvalidDataException("Movement purity or calculation terms failed.");
                    result=new JsonObject{["speed"]=movement.Speed,["climbSpeed"]=movement.ClimbSpeed,["swimSpeed"]=movement.SwimSpeed};
                }
                if(!JsonNode.DeepEquals(result,fixture!["expected"]))throw new InvalidDataException("Native defense expected value failed: "+fixture["id"]);
                output[section]!.AsArray().Add(new JsonObject{["id"]=fixture["id"]!.DeepClone(),["result"]=result});
            }
            Console.WriteLine(output.ToJsonString(options));return 0;
        }
        catch(Exception ex)when(ex is ArgumentException or InvalidDataException or JsonException or InvalidOperationException or IOException)
        {Console.Error.WriteLine("Defense comparison failed: "+ex.Message);return 1;}
    }
}
