using System.Text.Json.Nodes;
using System.Text.Json;
using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;
public sealed partial class NativeCharacterBuilder(ClassProgressionCatalog classes,RulesCatalogSnapshot catalog,CharacterBuildCatalog builds,CreationChoiceRules creation,ActiveFeatureRules features,SpellGrantRules grants,ClassResourceRules resources,WeaponCapabilityCatalog weapons,ExistingFeatValidationRules? ownedFeatRules=null):ICharacterDraftRules
{
    private readonly ExistingFeatValidationRules ownedFeats=ownedFeatRules??new(catalog,new(catalog));
    private static string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var text)?text:"";
    private static IEnumerable<string> Strings(JsonNode? n)=>n is JsonArray array?array.Select(Text).Where(s=>s.Length>0):[];

    public SettledBuild Recalculate(DraftCharacter character)
    {
        var calculation=Calculate(new CharacterDraft{Id="draft",Build=character.Build,Inventory=[..character.Equipment],IncludeDefaultEquipment=false},null,false,character.Scope);
        return new(calculation.Build,calculation.MaximumHp);
    }
    public CreationChoiceReport Describe(DraftCharacter character)=>Describe(character,true,FoundValues.None);
    public FeatRuleContext FeatContext(DraftCharacter character)=>FeatContext(character,true,FoundValues.None);

    private IReadOnlyList<WeaponProficiencyRule> WeaponProficiencies(NativeBuildSummary build)
    {
        var rules=new List<WeaponProficiencyRule>();foreach(var source in build.ProficiencySources.Where(p=>p.Kind==ProficiencySourceKind.Weapon))
        {
            if(source.Target.Equals("Simple Weapons",StringComparison.OrdinalIgnoreCase))rules.Add(new(WeaponProficiencyScope.Simple));
            else if(source.Target.Equals("Martial Weapons",StringComparison.OrdinalIgnoreCase))rules.Add(new(WeaponProficiencyScope.Martial));
            else if(source.SourceId=="class:Monk")rules.Add(new(WeaponProficiencyScope.MartialLight));
            else if(source.SourceId=="class:Rogue")rules.Add(new(WeaponProficiencyScope.MartialFinesseOrLight));
            else if(catalog.WeaponRecords.Any(w=>w.Name==source.Target))rules.Add(new(WeaponProficiencyScope.Named,source.Target));
        }
        return rules;
    }

    /// <summary>A draft's owned feat with the choices given for it; the feat's identity cannot change.</summary>
    internal BuildFacts RepairFeat(CharacterDraft draft,string instanceId,ChoiceValue value)
        =>RepairFeat(draft.Build,FeatContext(new DraftCharacter(draft.Build,draft.Inventory??StartingGear(draft)),draft.Inventory is not null,FoundValues.None),instanceId,value);
    /// <summary>A shown character's owned feat with the choices given for it, judged with what the character showed.</summary>
    internal BuildFacts RepairFeat(DraftCharacter character,FoundValues found,string instanceId,ChoiceValue value)
        =>RepairFeat(character.Build,FeatContext(character,false,found),instanceId,value);
    internal BuildFacts RepairFeat(BuildFacts facts,FeatRuleContext context,string instanceId,ChoiceValue value)
    {
        var descriptor=ownedFeats.DescribeChoices(facts,context).SingleOrDefault(f=>f.InstanceId==instanceId)??throw new CharacterWorkflowException("invalid_feat","Choose one supported existing feat instance.");
        if((value.At(["feat_id"])?.Text??"")!=descriptor.FeatId||(value.At(["mode"])?.Text??"") is not ("" or "feat"))throw new CharacterWorkflowException("invalid_feat","Repair the choices belonging to this feat.");
        var choices=new Dictionary<string,ChoiceValue>(value.At(["feat_choices"])?.Fields??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal);if(value.At(["feat_ability_choice"]) is {} ability)choices["ability"]=ability;
        return ownedFeats.ApplyChoices(facts,instanceId,choices);
    }
}
