using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Web.Services;

public static partial class WorkspaceProjection
{
    /// <summary>
    /// The character a calculation gives, around the character it edits (<paramref name="existing"/>): its HP in play,
    /// what it spent, its notes, effects and the rest of its play state.
    /// </summary>
    internal static CharacterState FromCalculation(CharacterCalculation c, CharacterState? existing = null)
    {
        if (!c.KnownClass) throw new ArgumentException("The character's class is not in the 2024 catalog.");
        var build = c.Build;
        var id = existing?.Id ?? c.Id;
        if (!Regex.IsMatch(id, @"\A[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}\z")) id = Guid.NewGuid().ToString("N");
        var equipment = existing?.Equipment ?? [.. c.Equipment];
        var previousGrants = existing?.Derived.GrantedSpells ?? [];
        var grantUses = GrantUses(c);
        var picks = build.Spells;
        var prepared = Present(picks?.Prepared);
        var known = Present(picks?.Known).Concat(Present(picks?.Cantrips)).Distinct().ToList();
        var book = Present(picks?.Spellbook).Concat(Present(c.Spellbook)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var granted = Present(c.GrantedSpellNames);
        var spellDefinitions = c.Spells.GroupBy(x => x.Id).Select(g => g.First()).ToList();
        foreach (var name in prepared.Concat(known).Concat(book).Concat(granted).Distinct())
            if (!spellDefinitions.Any(s => NormalizeName(s.Name) == NormalizeName(name)))
                spellDefinitions.Add(SpellCatalog.All.FirstOrDefault(s => NormalizeName(s.Name) == NormalizeName(name)) ?? new() { Id = Slug(name), Name = name, Description = "Imported spell. Its numerical effects are tracked manually until structured metadata is available.", Level = 1 });
        string SpellId(string name) => spellDefinitions.FirstOrDefault(s => NormalizeName(s.Name) == NormalizeName(name))?.Id ?? Slug(name);
        var className = build.ClassName; var level = build.Level;
        var resources = Resources(c, existing);
        var grantIds = granted.Select(SpellId).ToList();
        var namesWithSources = new HashSet<string>();
        var previousPools = existing?.Resources ?? [];
        var currentGrantPools = c.Grants.Select(g => g.ResourceId ?? "").ToHashSet(StringComparer.Ordinal);
        string Owner(string grantId) => c.GrantSources.FirstOrDefault(s => s.GrantId == grantId)?.SourceId ?? "";
        ResourcePool? PriorGrant(SpellGrantMetadata grant)
        {
            var resourceId = grant.ResourceId ?? ""; var exact = previousPools.FirstOrDefault(p => p.Id == resourceId); if (exact is not null) return exact;
            var owner = Owner(grant.Id);
            var removed = previousGrants.Where(old => !currentGrantPools.Contains(old.ResourceId) && old.BaseLevel == grant.BaseLevel)
                .Where(old => owner.Length > 0 && old.OwnerId.Length > 0 ? owner == old.OwnerId : old.Source == grant.Source)
                .Select(old => previousPools.FirstOrDefault(p => p.Id == old.ResourceId)).OfType<ResourcePool>().ToArray();
            // A renamed spell keeps the expenditure of its feature. Ambiguous old slots use the largest expenditure.
            return removed.OrderByDescending(p => p.Maximum - p.Remaining).FirstOrDefault();
        }
        foreach (var grant in c.Grants)
        {
            var name = grant.SpellName; if (name.Length == 0) continue;
            var basis = spellDefinitions.FirstOrDefault(s => NormalizeName(s.Name) == NormalizeName(name))
                ?? SpellCatalog.All.FirstOrDefault(s => NormalizeName(s.Name) == NormalizeName(name))
                ?? new SpellDefinition { Id = Slug(name), Name = name, Level = grant.BaseLevel };
            var alias = Slug(grant.Id);
            var resourceId = grant.ResourceId ?? "";
            var definition = basis with
            {
                Id = alias, Name = name, Source = grant.Source, CastingAbility = grant.Ability, CastingSource = grant.Source, GrantResourceId = resourceId.Length == 0 ? null : resourceId,
                AtWill = grant.AtWill, AllowsSlotCasting = grant.AllowsSlots, RitualOnly = grant.RitualOnly, Ritual = basis.Ritual || grant.RitualOnly
            };
            spellDefinitions.RemoveAll(s => s.Id == alias); spellDefinitions.Add(definition); grantIds.Add(alias); namesWithSources.Add(NormalizeName(name));
            if (grant.MaxUses > 0 && resourceId.Length > 0 && !resources.Any(r => r.Id == resourceId))
            {
                var prior = PriorGrant(grant);
                resources.Add(new()
                {
                    Id = resourceId, Name = name + " · " + definition.Source, Maximum = grant.MaxUses, SourceKind = "grant",
                    Remaining = prior is null ? grant.MaxUses : Math.Clamp(grant.MaxUses - (prior.Maximum - prior.Remaining), 0, grant.MaxUses),
                    Recovery = Recovery(grant.Recovery)
                });
            }
        }
        grantIds.RemoveAll(gid => !gid.StartsWith("grant-", StringComparison.Ordinal) && spellDefinitions.Any(s => s.Id == gid && namesWithSources.Contains(NormalizeName(s.Name))));
        var restored = existing?.Play;
        var castingAbility = c.SpellcastingAbility ?? (Enum.TryParse<Ability>(c.DerivedResources.GetValueOrDefault("spellcasting_ability")?.Text ?? "Wisdom", true, out var parsed) ? parsed : Ability.Wisdom);
        var p = c.Preparation;
        var policy = p is null
            ? new PreparationPolicy
            {
                CanChangeOnLongRest = new[] { "Cleric", "Druid", "Wizard", "Paladin", "Ranger" }.Contains(className), PreparedLimit = Math.Max(c.PreparedLimit, prepared.Count),
                ReplacementLimit = className is "Ranger" or "Paladin" ? 1 : null, FromSpellbook = className == "Wizard", EligibleSpellIds = [.. spellDefinitions.Where(s => s.Level > 0).Select(s => s.Id)]
            }
            : new PreparationPolicy
            {
                CanChangeCantripsOnLongRest = p.CanChangeCantripsOnLongRest, CantripReplacementLimit = p.CantripReplacementLimit,
                OwnedCantripSpellIds = [.. p.OwnedCantrips.Select(SpellId).Distinct()], EligibleCantripSpellIds = [.. p.EligibleCantrips.Select(SpellId).Distinct()],
                CanChangeOnLongRest = p.CanChangeOnLongRest, PreparedLimit = Math.Max(p.PreparedLimit, prepared.Count), ReplacementLimit = p.ReplacementLimit,
                FromSpellbook = className == "Wizard", EligibleSpellIds = [.. p.EligibleSpells.Select(SpellId).Distinct()]
            };
        var play = (restored ?? new PlayState()) with
        {
            SpellcastingAbility = castingAbility, HitDieSize = c.HitDie is 6 or 8 or 10 or 12 ? c.HitDie : 8,
            PreparedSpellIds = [.. prepared.Select(SpellId)], KnownSpellIds = [.. known.Select(SpellId)], SpellbookIds = [.. book.Select(SpellId)], GrantedSpellIds = [.. grantIds.Distinct()], Spells = spellDefinitions, Preparation = policy,
            UndoHistory = [], LastCast = null, LastRoll = null, CompletedOperationIds = []
        };
        play = ClassPlay(play, c, restored, resources);
        var maximum = Math.Max(1, c.MaximumHp);
        var defenses = (existing?.Defenses ?? []).Where(d => d.Source != "Character build").ToList();
        foreach (var (field, kind) in new[] { ("damage_resistances", "resistance"), ("damage_immunities", "immunity"), ("damage_vulnerabilities", "vulnerability"), ("condition_immunities", "conditionImmunity") })
            foreach (var type in Present(c.Facts.GetValueOrDefault(field)))
                if (!defenses.Any(d => d.Kind == kind && d.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
                    defenses.Add(new() { Kind = kind, Type = type, Source = "Character build" });
        var current = Math.Clamp(c.CurrentHp, 0, maximum);
        var character = new CharacterState
        {
            Id = id, Name = build.Name, Species = build.Species, ClassName = className, Subclass = build.Subclass, Level = level,
            CurrentHp = current, MaximumHp = maximum, TemporaryHp = c.TemporaryHp, ArmorClass = c.ArmorClass, Speed = c.Speed, Turn = existing?.Turn ?? 1,
            AbilityScores = Enum.GetValues<Ability>().ToDictionary(a => a, a => c.Scores.GetValueOrDefault(a, 10)),
            SaveProficiencies = [.. c.SaveProficiencies], SkillProficiencies = [.. c.SkillProficiencies], Expertise = [.. c.Expertise],
            Combat = existing?.Combat ?? new(), Sheet = existing?.Sheet ?? SheetRules.ImportState(build.ToCanonical(id), current),
            Equipment = equipment, Resources = resources, Notes = existing?.Notes ?? (string.IsNullOrWhiteSpace(c.Notes) ? [] : [new() { Id = Guid.NewGuid().ToString("N"), Title = "Imported notes", Text = c.Notes }]), Effects = existing?.Effects ?? [], Play = play, Build = build,
            Derived = Derived(c, grantUses), Features = [.. Present(c.Features)], Defenses = defenses,
            Exhaustion = existing?.Exhaustion ?? 0, KnownTarget = existing?.KnownTarget ?? new()
        };
        var adjusted = character with { StandingAdjustments = new(), StandingAdjustmentTerms = new() };
        foreach (var a in Enum.GetValues<Ability>())
            if (c.Modifiers.TryGetValue(a, out var modifier)) adjusted.StandingAdjustments["ability:" + a] = modifier - CharacterCalculations.Ability(adjusted, a).Total;
        foreach (var a in Enum.GetValues<Ability>())
            if (c.SaveBonuses.TryGetValue(a, out var save)) adjusted.StandingAdjustments["save:" + a] = save - CharacterCalculations.Save(adjusted with { Exhaustion = 0 }, a).Total;
        foreach (var pair in SheetSkills)
            if (c.SkillBonuses.TryGetValue(pair.Key, out var skill)) adjusted.StandingAdjustments["skill:" + pair.Key] = skill - CharacterCalculations.Skill(adjusted with { Exhaustion = 0 }, pair.Key, pair.Value).Total;
        adjusted.StandingAdjustments["initiative"] = c.Initiative - CharacterCalculations.Initiative(adjusted with { Exhaustion = 0 }).Total;
        adjusted = adjusted with { StandingAdjustmentTerms = c.StandingTerms.ToDictionary(t => t.Key, t => t.Value.ToList()) };
        WorkspaceRules.Validate(adjusted); return adjusted;
    }

    /// <summary>Character keys the rules calculate, beside the build's own; an upload's values for them are recalculated, not dropped.</summary>
    public static readonly IReadOnlySet<string> CalculatedKeys = CanonicalCharacterRules.Create().Select(p => p.Key).Concat([
        "folio_derived_inventory", "species_traits", "size", "background_feature", "origin_feats", "senses", "damage_resistances", "damage_immunities", "damage_vulnerabilities", "condition_immunities",
        "ability_score_adjustment_bonuses", "saving_throw_bonuses", "skill_bonuses", "folio_native_standing_terms", "hit_point_details", "armor_class_details", "speed_details",
        "spell_grant_details", "subclass_granted_spells", "species_spell_grants", "folio_species_auto_grants", "spellcasting_ability", "prepared_spell_limit", "spell_metadata", "spell_descriptions",
        "spellcasting_sources", "spellbook_spells", "mystic_arcanum", "folio_total_level", "folio_additional_class"]).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// A character as an export file: its build, its play facts in Herohold's sidecars, and what the rules calculated for it,
    /// so another copy of Herohold can review it and other tools can read its numbers.
    /// </summary>
    public static JsonObject ExportDocument(CharacterState c)
    {
        var document = ToCanonical(c);
        static JsonArray Names(IEnumerable<string> names) => new([.. names.Distinct(StringComparer.Ordinal).Select(n => (JsonNode?)JsonValue.Create(n))]);
        document["languages"] = Names(c.Derived.Languages); document["armor_proficiencies"] = Names(c.Derived.ArmorProficiencies);
        document["weapon_proficiencies"] = Names(c.Derived.WeaponProficiencies); document["tool_proficiencies"] = Names(c.Derived.ToolProficiencies);
        document["features"] = Names(c.Features); document["species_traits"] = Names(c.Derived.SpeciesTraits); document["origin_feats"] = Names(c.Derived.OriginFeats);
        document["size"] = c.Derived.Size; document["proficiency_bonus"] = CharacterCalculations.ProficiencyBonus(c.Level);
        document["senses"] = new JsonArray([.. c.Derived.Senses.Select(s => (JsonNode?)new JsonObject { ["type"] = s.Type, ["range"] = s.Range, ["sources"] = Names(s.Sources) })]);
        NativeExportOverlay.Apply(document, NativeExportProjection.Create(c));
        document["spellcasting_sources"] = new JsonArray([.. c.Derived.SpellcastingSources.Select(s => (JsonNode?)new JsonObject { ["source_name"] = s.SourceName, ["source_type"] = s.SourceType, ["ability"] = s.Ability.ToString(), ["spell_save_dc"] = s.SpellSaveDc, ["spell_attack_bonus"] = s.SpellAttackBonus })]);
        return CanonicalCharacterRules.Migrate(document);
    }

    /// <summary>
    /// A saved character's build as it is stored: its identity and scores, the spells it has prepared, known and in its
    /// spellbook in play, its sheet's XP, inspiration and coins, and its hand-entered defenses as they stand now.
    /// </summary>
    public static BuildFacts BuildOf(CharacterState c)
    {
        var build = (c.Build ?? new BuildFacts()) with
        {
            Name = c.Name, Species = c.Species, ClassName = c.ClassName, Subclass = c.Subclass, Level = c.Level,
            BaseAbilityScores = c.Build?.BaseAbilityScores is { } scores ? Enum.GetValues<Ability>().Where(scores.ContainsKey).ToDictionary(a => a, a => scores[a]) : Enum.GetValues<Ability>().Where(c.AbilityScores.ContainsKey).ToDictionary(a => a, a => c.AbilityScores[a])
        };
        string Name(string id) => c.Play.Spells.FirstOrDefault(s => s.Id == id)?.Name ?? id;
        int? Level(string id) => c.Play.Spells.FirstOrDefault(s => s.Id == id)?.Level;
        if (c.Play.Spells.Count > 0)
        {
            var automaticBook = c.Derived.Spellbook.Except(c.Build?.Spells?.Spellbook ?? [], StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            build = build with
            {
                Spells = new()
                {
                    Cantrips = [.. c.Play.KnownSpellIds.Where(id => Level(id) == 0).Select(Name)], Known = [.. c.Play.KnownSpellIds.Where(id => Level(id) != 0).Select(Name)],
                    Prepared = [.. c.Play.PreparedSpellIds.Select(Name)], Spellbook = [.. c.Play.SpellbookIds.Where(id => !automaticBook.Contains(Name(id))).Select(Name)]
                }
            };
        }
        if (c.Play.SpellSources.Count > 0 && build.ClassTracks is { } tracks)
            build = build with
            {
                ClassTracks = [.. tracks.Select(track => c.Play.SpellSources.FirstOrDefault(s => s.SourceKind == "class" && s.ClassTrackId == track.Id) is not { } source ? track : track with
                {
                    Choices = new(track.Choices, StringComparer.Ordinal)
                    {
                        ["spells"] = Spells(track.Choices.GetValueOrDefault("spells"), source)
                    }
                })]
            };
        ChoiceValue Spells(ChoiceValue? existing, SpellSourceState source)
        {
            ChoiceValue List(IEnumerable<string> ids) => ChoiceValue.ListOf(ids.Except(source.GrantedSpellIds).Select(id => ChoiceValue.Of(Name(id))));
            var fields = new Dictionary<string, ChoiceValue>(existing?.Fields ?? new Dictionary<string, ChoiceValue>(), StringComparer.Ordinal)
            {
                ["prepared"] = List(source.PreparedSpellIds), ["known"] = List(source.KnownSpellIds.Where(id => Level(id) != 0)),
                ["cantrips"] = List(source.KnownSpellIds.Where(id => Level(id) == 0)), ["spellbook"] = List(source.SpellbookIds)
            };
            return ChoiceValue.Record(fields);
        }
        if (!SheetRules.HasUnresolvedExperience(c)) build = build with { ImportedExperiencePoints = ChoiceValue.Of(SheetRules.ExperiencePoints(c)) };
        build = build with { ImportedHeroicInspiration = ChoiceValue.Of(SheetRules.HasHeroicInspiration(c)) };
        if (!SheetRules.HasUnresolvedCoins(c))
        {
            var purse = SheetRules.Purse(c);
            var coins = new Dictionary<string, ChoiceValue>(build.ImportedCoins?.Fields ?? new Dictionary<string, ChoiceValue>(), StringComparer.Ordinal)
            { ["cp"] = ChoiceValue.Of(purse.Cp), ["sp"] = ChoiceValue.Of(purse.Sp), ["ep"] = ChoiceValue.Of(purse.Ep), ["gp"] = ChoiceValue.Of(purse.Gp), ["pp"] = ChoiceValue.Of(purse.Pp) };
            build = build with { ImportedCoins = ChoiceValue.Record(coins) };
        }
        if (build.ManualFacts is { } manual)
        {
            List<string> Retained(List<string> prior, string kind)
            {
                var current = c.Defenses.Where(d => d.Kind == kind).ToArray(); var names = current.Select(d => d.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return [.. prior.Where(name => names.Contains(name, StringComparer.OrdinalIgnoreCase)).Concat(current.Where(d => d.Source != "Character build").Select(d => d.Type)).Distinct(StringComparer.OrdinalIgnoreCase)];
            }
            build = build with
            {
                ManualFacts = manual with
                {
                    DamageResistances = Retained(manual.DamageResistances, "resistance"), DamageImmunities = Retained(manual.DamageImmunities, "immunity"),
                    DamageVulnerabilities = Retained(manual.DamageVulnerabilities, "vulnerability"), ConditionImmunities = Retained(manual.ConditionImmunities, "conditionImmunity")
                }
            };
        }
        return build;
    }

    private static List<string> Present(IEnumerable<string>? names) => [.. (names ?? []).Where(n => n.Length > 0)];
    private static RecoveryRest Recovery(string recovery) => recovery.Contains("short", StringComparison.OrdinalIgnoreCase) ? RecoveryRest.Short : recovery.Contains("long", StringComparison.OrdinalIgnoreCase) ? RecoveryRest.Long : RecoveryRest.Manual;

    private static List<GrantedSpellUse> GrantUses(CharacterCalculation c)
        => [.. c.Grants.Select(g => new GrantedSpellUse { Id = g.Id, ResourceId = g.ResourceId ?? "", BaseLevel = g.BaseLevel, Source = g.Source, OwnerId = c.GrantSources.FirstOrDefault(s => s.GrantId == g.Id)?.SourceId ?? "" })];

    /// <summary>The calculation's resources with what was spent of each: a pool keeps its spent uses, a hand-made pool stays as it was.</summary>
    private static List<ResourcePool> Resources(CharacterCalculation c, CharacterState? existing)
    {
        var priorPools = existing?.Resources ?? [];
        var mapped = new List<ResourcePool>();
        foreach (var record in c.Resources)
        {
            if (record.Maximum <= 0) continue;
            var prior = priorPools.FirstOrDefault(r => r.Id == record.Id);
            if (prior?.SourceKind == "manual") { mapped.Add(prior); continue; }
            mapped.Add(new()
            {
                Id = record.Id, SourceId = string.IsNullOrEmpty(record.SourceId) ? null : record.SourceId, Name = record.Name, Maximum = record.Maximum, SourceKind = "build",
                Remaining = prior is null ? record.Maximum : Math.Clamp(record.Maximum - (prior.Maximum - prior.Remaining), 0, record.Maximum),
                Recovery = Recovery(record.Recovery), SlotLevel = record.SlotLevel, ShortRestRecoveryAmount = record.ShortRestRecoveryAmount
            });
        }
        foreach (var manual in priorPools.Where(r => r.SourceKind == "manual"))
            if (!mapped.Any(r => r.Id == manual.Id)) mapped.Add(manual);
        return mapped;
    }

    /// <summary>A multiclass character's spell sources and Hit Dice pools, keeping what each pool spent.</summary>
    private static PlayState ClassPlay(PlayState play, CharacterCalculation c, PlayState? restored, List<ResourcePool> resources)
    {
        if (c.SpellSources is not { } sources) return play;
        var owned = c.OwnedSpells ?? [];
        var pools = (c.HitDiePools ?? []).Select(pool =>
        {
            var old = restored?.HitDiePools.SingleOrDefault(p => p.Id == pool.Id);
            var legacy = restored?.HitDiePools.Count == 0 && restored.HitDieSize == pool.Sides ? resources.SingleOrDefault(p => p.Id == "hitdice") : null;
            var spent = old is not null ? old.Maximum - old.Remaining : legacy is not null ? legacy.Maximum - legacy.Remaining : 0;
            return pool with { Remaining = Math.Clamp(pool.Maximum - spent, 0, pool.Maximum), ClassTrackIds = [.. pool.ClassTrackIds] };
        }).ToList();
        var definitions = play.Spells.Where(s => !owned.Any(o => o.Id == s.Id)).Concat(owned).ToList();
        foreach (var source in sources)
            foreach (var spellId in source.PreparedSpellIds.Concat(source.KnownSpellIds).Concat(source.SpellbookIds).Concat(source.GrantedSpellIds))
                if (!definitions.Any(s => s.Id == spellId)) throw new ArgumentException("A spell source references an unavailable owned spell definition.");
        var poolIndex = resources.FindIndex(p => p.Id == "hitdice");
        if (poolIndex >= 0 && pools.Count > 0) resources[poolIndex] = resources[poolIndex] with { Maximum = pools.Sum(p => p.Maximum), Remaining = pools.Sum(p => p.Remaining) };
        return play with
        {
            SpellSources = sources, HitDiePools = pools, Spells = definitions,
            PreparedSpellIds = [.. sources.SelectMany(s => s.PreparedSpellIds).Distinct()], KnownSpellIds = [.. sources.SelectMany(s => s.KnownSpellIds).Distinct()],
            SpellbookIds = [.. sources.SelectMany(s => s.SpellbookIds).Distinct()], GrantedSpellIds = [.. sources.SelectMany(s => s.GrantedSpellIds).Distinct()]
        };
    }

    /// <summary>The sheet a calculation shows: attacks, senses, armor, spellcasting, features and proficiencies.</summary>
    private static DerivedSheet Derived(CharacterCalculation c, List<GrantedSpellUse> grantUses)
    {
        var details = c.FeatureDetails.ToList();
        static List<string> Names(IEnumerable<string> names) => [.. names.Where(n => n.Length > 0).Distinct()];
        var traits = Names(c.SpeciesTraits); var origins = Names(c.OriginFeats);
        List<string> Profile(string field) => [.. Present(c.Facts.GetValueOrDefault(field)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase)];
        return new()
        {
            Attacks = [.. c.Attacks.Select(a => a with { AbilityCandidates = [.. a.AbilityCandidates] })],
            AttackActionCount = c.AttackActionCount is >= 1 and <= 4 ? c.AttackActionCount : null,
            FeatureDetails = details, FeatureDescriptions = [], SpeciesTraits = traits, OriginFeats = origins,
            Senses = [.. c.Senses.Where(s => s.Type.Length > 0).Select(s => s with { Sources = [.. s.Sources] })], ArmorClass = c.Armor, SpellcastingSources = [.. c.CastingSources],
            Size = c.Size, AbilityAdjustmentBonuses = Enum.GetValues<Ability>().Where(c.AdjustmentBonuses.ContainsKey).ToDictionary(a => a, a => c.AdjustmentBonuses[a]),
            Languages = [.. c.Languages.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase)],
            ArmorProficiencies = Profile("armor_proficiencies"), WeaponProficiencies = Profile("weapon_proficiencies"), ToolProficiencies = Profile("tool_proficiencies"),
            Spellbook = [.. Present(c.Spellbook)], GrantedSpells = grantUses
        };
    }
}
