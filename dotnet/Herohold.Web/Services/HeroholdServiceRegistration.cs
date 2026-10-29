using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Documents;
using Herohold.Rules;
using Herohold.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Herohold.Web.Services;

/// <summary>The rules, catalogs and character workflow, built from the local rules data. Shared by the host and tests.</summary>
public static class HeroholdServiceRegistration
{
    public static IServiceCollection AddHeroholdRules(this IServiceCollection services, string repositoryRoot, string pdfWorkerAssembly)
    {
        var catalogDirectory = Path.Combine(repositoryRoot, "data", "imported", "5etools");
        var rawDirectory = Path.Combine(repositoryRoot, "data", "raw", "5etools");
        string ReadCatalog(string file) => File.ReadAllText(Path.Combine(catalogDirectory, file + ".json"));
        string ReadRaw(string file) => File.ReadAllText(Path.Combine(rawDirectory, file));

        var classesJson = ReadCatalog("classes");
        var classes = ClassProgressionCatalog.Parse(classesJson);
        var speciesJson = ReadCatalog("species");
        var backgroundsJson = ReadCatalog("backgrounds");
        var rawSpells = ReadRaw(Path.Combine("spells", "spells-xphb.json"));
        var snapshot = RulesCatalogSnapshot.Parse(classesJson, speciesJson, backgroundsJson, ReadCatalog("feats"), ReadCatalog("spells"), ReadCatalog("equipment"), ReadCatalog("weapons"), rawSpells);
        var choices = new ClassChoiceRules(classes, snapshot, BeastChoiceCatalog.Parse(ReadCatalog("beasts")), SpellCapabilityCatalog.Parse(rawSpells));
        var ruleData = RulesReference.Parse(ReadCatalog("rules"));
        var speciesSources = SpeciesSourceCatalog.Parse(ReadRaw("races.json"));
        var tools = ToolCatalog.Parse(ReadRaw("items-base.json"), ReadRaw("items.json"));

        services.AddSingleton(classes);
        services.AddSingleton(CharacterBuildCatalog.Parse(classes, backgroundsJson, speciesJson));
        services.AddSingleton(snapshot);
        services.AddSingleton(choices);
        services.AddSingleton(tools);
        services.AddSingleton(new CreationChoiceRules(snapshot, classes, choices, ruleData, speciesSources, tools));
        services.AddSingleton(new ActiveFeatureRules(snapshot, classes, choices, ruleData, speciesSources));
        services.AddSingleton(new SpellGrantRules(classes, snapshot, RawSpeciesSpellCatalog.From(speciesSources, snapshot)));
        services.AddSingleton(new ClassResourceRules(classes));
        services.AddSingleton(WeaponCapabilityCatalog.Parse(ReadRaw("items-base.json"), snapshot));
        services.AddSingleton(new FeatSelectionRules(snapshot, NativeSelectableFeatCatalog.Parse(ReadRaw("feats.json")).AdditionalDefinitions, tools));
        services.AddSingleton<ExistingFeatValidationRules>();
        services.AddSingleton(new CharacterEditRules(snapshot));
        services.AddSingleton<NativeCharacterBuilder>();
        services.AddSingleton<ICharacterDraftRules>(provider => provider.GetRequiredService<NativeCharacterBuilder>());
        services.AddSingleton<NativeLevelUpRules>();
        services.AddSingleton<IPdfService>(new PdfWorkerClient(pdfWorkerAssembly));
        services.AddSingleton<NativePdfImportReviewService>();
        services.AddSingleton<ICharacterRules, CharacterRules>();
        return services;
    }

    /// <summary>Characters in the SQLite database at <paramref name="databasePath"/>, recalculated by the rules on load.</summary>
    public static IServiceCollection AddHeroholdStorage(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton<ICharacterRecordStore>(new SqliteCharacterRecordStore(databasePath));
        services.AddSingleton<CharacterHydrator>();
        services.AddSingleton<IWorkspaceStore, RecordWorkspaceStore>();
        return services;
    }
}
