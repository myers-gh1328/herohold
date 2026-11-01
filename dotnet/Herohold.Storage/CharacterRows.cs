using Herohold.Core;

namespace Herohold.Storage;

/// <summary>Every row of one character, and the exact conversion to and from <see cref="CharacterRecord"/>.</summary>
public sealed class CharacterRows
{
    public CharacterRow Character { get; set; } = new();
    public List<ListValueRow> Lists { get; } = [];
    public List<ChoiceRow> Choices { get; } = [];
    public List<FeatRow> Feats { get; } = [];
    public List<AbilityAdjustmentRow> AbilityAdjustments { get; } = [];
    public List<FeatureConfigurationRow> FeatureConfigurations { get; } = [];
    public List<HpGainRow> HpGains { get; } = [];
    public List<HpRollRow> HpRolls { get; } = [];
    public List<LevelUpRow> LevelUps { get; } = [];
    public List<ResolvedChoiceRow> ResolvedChoices { get; } = [];
    public List<ClassTrackRow> ClassTracks { get; } = [];
    public List<ItemRow> Items { get; } = [];
    /// <summary>The definitions of the catalog items <see cref="Items"/> point at, by catalog item id.</summary>
    public Dictionary<string, CatalogItem> Catalog { get; } = new(StringComparer.Ordinal);
    public List<DefenseRow> Defenses { get; } = [];
    public List<EffectRow> Effects { get; } = [];
    public List<JournalNoteRow> JournalNotes { get; } = [];
    public List<MovementModeRow> MovementModes { get; } = [];
    public List<CompanionRow> Companions { get; } = [];
    public List<CompanionActionRow> CompanionActions { get; } = [];
    public List<ResourceRow> Resources { get; } = [];
    public List<HitDiePoolRow> HitDiePools { get; } = [];
    public List<SpellSourceRow> SpellSources { get; } = [];
    public List<CombatRow> Combats { get; } = [];
    public List<CombatTimerRow> CombatTimers { get; } = [];
    public List<CombatGrantRow> CombatGrants { get; } = [];
    public List<RollRow> Rolls { get; } = [];
    public List<DamageTermRow> DamageTerms { get; } = [];
    public List<CastRow> Casts { get; } = [];
    public List<PlaySnapshotRow> PlaySnapshots { get; } = [];
    public List<UndoEntryRow> UndoEntries { get; } = [];

    private const string Self = "character";
    private static int BeforeSlot(int entry) => 2 * entry + 1;
    private static int AfterSlot(int entry) => 2 * entry + 2;

    // ---------- record -> rows ----------

    public static CharacterRows From(CharacterRecord record, DateTimeOffset updatedAt)
    {
        var rows = new CharacterRows();
        var id = record.Id;
        var b = record.Build;
        var f = record.Facts;
        var sheet = f.Sheet;
        var profile = sheet.Profile;
        var scores = b.BaseAbilityScores;
        rows.Character = new CharacterRow
        {
            Id = id, OwnerId = record.OwnerId, RulesSource = record.RulesSource, Revision = record.Revision, UpdatedAt = updatedAt,
            Name = b.Name, ClassName = b.ClassName, Subclass = b.Subclass, Level = b.Level, Species = b.Species, Background = b.Background,
            AbilityScoreMethod = b.AbilityScoreMethod,
            BaseStrength = Score(scores, Ability.Strength), BaseDexterity = Score(scores, Ability.Dexterity), BaseConstitution = Score(scores, Ability.Constitution),
            BaseIntelligence = Score(scores, Ability.Intelligence), BaseWisdom = Score(scores, Ability.Wisdom), BaseCharisma = Score(scores, Ability.Charisma),
            EquipmentMigrationPending = b.EquipmentMigrationPending,
            HasPresentation = b.Presentation is not null, PrimaryWeapon = b.Presentation?.PrimaryWeapon ?? "", FeaturedCantrip = b.Presentation?.FeaturedCantrip ?? "",
            ImportMaximumHpAdjustment = b.ImportOverrides?.MaximumHpAdjustment,
            HasImportProvenance = b.ImportProvenance is not null, ImportSourceKind = b.ImportProvenance?.SourceKind, ImportStatus = b.ImportProvenance?.Status,
            ImportedAt = b.ImportProvenance?.ImportedAt, ImportAbilityHistoryReconstructed = b.ImportProvenance?.AbilityHistoryReconstructed,
            ImportHpHistoryReconstructed = b.ImportProvenance?.HpHistoryReconstructed,
            HasSpellPicks = b.Spells is not null, ClassTracksVersion = b.ClassTracksVersion, InitialClassTrackId = b.InitialClassTrackId,
            JournalVersion = b.Journal?.Version, CustomJournalVersion = b.CustomJournal?.Version, JournalCleanupV1 = b.JournalCleanupV1,
            HasManualFacts = b.ManualFacts is not null,
            CurrentHp = f.CurrentHp, TemporaryHp = f.TemporaryHp, Turn = f.Turn, Exhaustion = f.Exhaustion,
            DreadUsedThisTurn = f.DreadUsedThisTurn, HunterTarget = f.HunterTarget,
            KnownTargetName = f.KnownTarget.Name, KnownTargetKnowledge = f.KnownTarget.Knowledge,
            ExperiencePoints = sheet.ExperiencePoints, HeroicInspiration = sheet.HeroicInspiration,
            HasCoins = sheet.Coins is not null, CoinsCp = sheet.Coins?.Cp ?? 0, CoinsSp = sheet.Coins?.Sp ?? 0, CoinsEp = sheet.Coins?.Ep ?? 0, CoinsGp = sheet.Coins?.Gp ?? 0, CoinsPp = sheet.Coins?.Pp ?? 0,
            DeathSaveSuccesses = sheet.DeathSaves.Successes, DeathSaveFailures = sheet.DeathSaves.Failures, Stable = sheet.DeathSaves.Stable, Dead = sheet.DeathSaves.Dead,
            CapacityOverrideLb = sheet.Adventure.CapacityOverrideLb, CapacitySource = sheet.Adventure.CapacitySource, CarryCoins = sheet.Adventure.CarryCoins, CountAmmunition = sheet.CountAmmunition,
            ProfileBackground = profile.Background, ProfileSize = profile.Size, Appearance = profile.Appearance, Backstory = profile.Backstory, Personality = profile.Personality,
            Alignment = profile.Alignment, PlayerName = profile.PlayerName, Age = profile.Age, Height = profile.Height, Weight = profile.Weight, Eyes = profile.Eyes,
            Skin = profile.Skin, Hair = profile.Hair, Ideals = profile.Ideals, Bonds = profile.Bonds, Flaws = profile.Flaws, Allies = profile.Allies,
            Organizations = profile.Organizations, Treasure = profile.Treasure
        };

        // Build lists and maps
        rows.Pairs(0, Self, "class_levels", b.ClassLevels?.Select(p => ((string?)p.Key, (string?)null, (int?)p.Value)));
        rows.Pairs(0, Self, "ability_score_assignments", b.AbilityScoreAssignments?.Select(p => ((string?)p.Key, (string?)null, (int?)p.Value)));
        rows.Numbers(0, Self, "rolled_ability_scores", b.RolledAbilityScores);
        rows.Names(0, Self, "origin_languages", b.OriginLanguages);
        rows.Names(0, Self, "spells.cantrips", b.Spells?.Cantrips);
        rows.Names(0, Self, "spells.known", b.Spells?.Known);
        rows.Names(0, Self, "spells.prepared", b.Spells?.Prepared);
        rows.Names(0, Self, "spells.spellbook", b.Spells?.Spellbook);
        rows.Names(0, Self, "spellbook_spells", b.SpellbookSpells);
        rows.Names(0, Self, "weapon_masteries", b.WeaponMasteries);
        rows.Pairs(0, Self, "weapon_wielding_modes", b.Presentation?.WeaponWieldingModes.Select(p => ((string?)p.Key, (string?)p.Value, (int?)null)));
        rows.Names(0, Self, "completed_level_up_transactions", b.CompletedLevelUpTransactions);
        rows.Names(0, Self, "import_warnings", b.ImportProvenance?.Warnings);
        rows.Names(0, Self, "migration_warnings", b.MigrationWarnings);
        rows.Pairs(0, Self, "journal_categories", b.Journal?.Categories.Select(p => ((string?)p.Key, (string?)p.Value, (int?)null)));
        rows.Names(0, Self, "custom_journal_categories", b.CustomJournal?.Categories);
        rows.Pairs(0, Self, "custom_journal_notes", b.CustomJournal?.Notes.Select(p => ((string?)p.Key, (string?)p.Value, (int?)null)));
        rows.Names(0, Self, "archived_journal_notes", b.ArchivedJournalNotes);
        var manual = b.ManualFacts;
        rows.Names(0, Self, "manual.tool_proficiencies", manual?.ToolProficiencies);
        rows.Names(0, Self, "manual.armor_proficiencies", manual?.ArmorProficiencies);
        rows.Names(0, Self, "manual.weapon_proficiencies", manual?.WeaponProficiencies);
        rows.Names(0, Self, "manual.damage_resistances", manual?.DamageResistances);
        rows.Names(0, Self, "manual.damage_immunities", manual?.DamageImmunities);
        rows.Names(0, Self, "manual.damage_vulnerabilities", manual?.DamageVulnerabilities);
        rows.Names(0, Self, "manual.condition_immunities", manual?.ConditionImmunities);
        rows.Names(0, Self, "manual.languages", b.ManualLanguages);
        rows.Names(0, Self, "manual.skills", b.ManualSkills);
        rows.Names(0, Self, "manual.expertise", b.ManualExpertise);
        rows.Names(0, Self, "manual.saves", b.ManualSaves);
        rows.Choice("class_options", b.ClassOptions);
        rows.Choice("species_options", b.SpeciesOptions);
        rows.Choice("background_options", b.BackgroundOptions);
        rows.Choice("origin_feat_choices", b.OriginFeatChoices);
        rows.Choice("folio_origin_feat_choices", b.HeroholdOriginFeatChoices);
        rows.Choice("folio_origin_choice_owners", b.OriginChoiceOwners);
        rows.Choice("pdf_passthrough_fields", b.PdfPassthroughFields);
        rows.Choice("movement_modes", b.ImportedMovementModes);
        foreach (var (owner, value) in new[] { ("coins", b.ImportedCoins), ("currency", b.ImportedCurrency), ("experience_points", b.ImportedExperiencePoints), ("xp", b.ImportedXp), ("heroic_inspiration", b.ImportedHeroicInspiration) })
            if (value is not null) rows.ChoiceNode("imported:" + owner, "", 0, value);
        rows.Present(0, Self, "general_feats", b.Feats);
        foreach (var (feat, i) in (b.Feats ?? []).Select((x, i) => (x, i)))
        {
            rows.Feats.Add(new() { CharacterId = id, Position = i, InstanceId = feat.InstanceId, FeatId = feat.FeatId, Name = feat.Name, Repeatable = feat.Repeatable,
                SourceType = feat.Source.Type, SourceClassName = feat.Source.ClassName, SourceLevel = feat.Source.Level, SourceTransactionId = feat.Source.TransactionId,
                LevelTaken = feat.LevelTaken, AbilityIncreaseInBase = feat.AbilityIncreaseInBase });
            rows.Choice("feat:" + i, feat.Choices);
            if (feat.LegacyChoiceHistory is { } history) rows.ChoiceNode("feat:" + i + ":history", "", 0, ChoiceValue.ListOf(history.Select(ChoiceValue.Record)));
        }
        rows.Present(0, Self, "ability_score_adjustments", b.AbilityScoreAdjustments);
        foreach (var (adjustment, i) in (b.AbilityScoreAdjustments ?? []).Select((x, i) => (x, i)))
        {
            rows.AbilityAdjustments.Add(new() { CharacterId = id, Position = i, Type = adjustment.Type, SourceType = adjustment.SourceType, SourceId = adjustment.SourceId, InstanceId = adjustment.InstanceId, SourceFeat = adjustment.SourceFeat,
                SourceName = adjustment.SourceName, SourceClass = adjustment.SourceClass, SourceLevel = adjustment.SourceLevel, HasSource = adjustment.Source is not null,
                SourceRefType = adjustment.Source?.Type, SourceRefClassName = adjustment.Source?.ClassName, SourceRefLevel = adjustment.Source?.Level,
                SourceRefTransactionId = adjustment.Source?.TransactionId, LevelUpChoiceId = adjustment.LevelUpChoiceId, FeatInstanceId = adjustment.FeatInstanceId, Maximum = adjustment.Maximum });
            rows.Pairs(0, "adjustment:" + i, "amounts", adjustment.Adjustments.Select(p => ((string?)p.Key.ToString(), (string?)null, (int?)p.Value)));
        }
        rows.Present(0, Self, "feature_configurations", b.FeatureConfigurations);
        foreach (var (c, i) in (b.FeatureConfigurations ?? []).Select((x, i) => (x, i)))
            rows.FeatureConfigurations.Add(new() { CharacterId = id, Position = i, FeatureId = c.FeatureId, CurrentValue = c.CurrentValue });
        rows.Present(0, Self, "hp_history", b.HpHistory);
        foreach (var (g, i) in (b.HpHistory ?? []).Select((x, i) => (x, i)))
            rows.HpGains.Add(new() { CharacterId = id, Position = i, CharacterLevel = g.CharacterLevel, ClassName = g.ClassName, Method = g.Method, BaseGain = g.BaseGain });
        rows.Present(0, Self, "hp_rolls", b.HpRolls);
        foreach (var (r, i) in (b.HpRolls ?? []).Select((x, i) => (x, i)))
            rows.HpRolls.Add(new() { CharacterId = id, Position = i, Level = r.Level, Value = r.Value, Mode = r.Mode });
        rows.Present(0, Self, "level_history", b.LevelHistory);
        foreach (var (l, i) in (b.LevelHistory ?? []).Select((x, i) => (x, i)))
        {
            rows.LevelUps.Add(new() { CharacterId = id, Position = i, ClassName = l.ClassName, FromLevel = l.FromLevel, ToLevel = l.ToLevel, TransactionId = l.TransactionId, HpIncrease = l.HpIncrease, Source = l.Source });
            rows.Names(0, "level_up:" + i, "automatic_features", l.AutomaticFeatures);
            foreach (var (choice, j) in l.ResolvedChoices.Select((x, j) => (x, j)))
            {
                rows.ResolvedChoices.Add(new() { CharacterId = id, LevelUpPosition = i, Position = j, ChoiceId = choice.Id });
                rows.ChoiceNode("level_up:" + i + ":" + j, "", 0, choice.Value);
            }
        }
        rows.Present(0, Self, "folio_class_tracks", b.ClassTracks);
        foreach (var (t, i) in (b.ClassTracks ?? []).Select((x, i) => (x, i)))
        {
            rows.ClassTracks.Add(new() { CharacterId = id, Position = i, TrackId = t.Id, ClassName = t.ClassName, Level = t.Level, RulesSource = t.RulesSource, Subclass = t.Subclass });
            rows.Choice("track:" + i, t.Choices);
        }

        // Play facts
        foreach (var (item, i) in f.Equipment.Select((x, i) => (x, i)))
        {
            if (item.CatalogItemId.Length == 0) throw new ArgumentException("An item must be linked to the item catalog before it is stored.");
            rows.Items.Add(new() { CharacterId = id, Position = i, ItemId = item.Id, CatalogItemId = item.CatalogItemId, Quantity = item.Quantity, Equipped = item.Equipped,
                Attuned = item.Attuned, Carried = item.Carried, ChargeRemaining = item.Charges?.Remaining ?? 0, ScrollSpell = item.ScrollSpell });
            rows.Catalog.TryAdd(item.CatalogItemId, new CatalogItem { Id = item.CatalogItemId, Definition = CatalogItem.DefinitionOf(item) });
        }
        foreach (var (note, i) in f.Notes.Select((x, i) => (x, i)))
            rows.JournalNotes.Add(new() { CharacterId = id, Position = i, NoteId = note.Id, Title = note.Title, Text = note.Text, CreatedAt = note.CreatedAt });
        rows.AddEffects(id, 0, Self, f.Effects);
        rows.AddDefenses(id, Self, f.Defenses);
        rows.AddDefenses(id, "target", f.KnownTarget.Defenses);
        rows.Names(0, Self, "profile.languages", profile.Languages);
        rows.Names(0, Self, "profile.armor_training", profile.ArmorTraining);
        rows.Names(0, Self, "profile.weapon_proficiencies", profile.WeaponProficiencies);
        rows.Names(0, Self, "profile.tool_proficiencies", profile.ToolProficiencies);
        foreach (var (m, i) in sheet.Adventure.Movement.Select((x, i) => (x, i)))
            rows.MovementModes.Add(new() { CharacterId = id, Position = i, Kind = m.Kind, Speed = m.Speed, Source = m.Source, Note = m.Note });
        foreach (var (c, i) in sheet.Adventure.Companions.Select((x, i) => (x, i)))
        {
            rows.Companions.Add(new() { CharacterId = id, Position = i, CompanionId = c.Id, Name = c.Name, Kind = c.Kind, ArmorClass = c.ArmorClass, MaximumHp = c.MaximumHp,
                CurrentHp = c.CurrentHp, TemporaryHp = c.TemporaryHp, Speed = c.Speed, Source = c.Source, Notes = c.Notes });
            foreach (var (a, j) in c.Actions.Select((x, j) => (x, j)))
                rows.CompanionActions.Add(new() { CharacterId = id, CompanionPosition = i, Position = j, Name = a.Name, Rules = a.Rules });
            rows.AddResources(id, 0, "companion:" + i, c.Resources);
            rows.Names(0, "companion:" + i, "conditions", c.Conditions);
        }
        rows.AddResources(id, 0, Self, f.Resources);
        rows.AddHitDiePools(id, 0, f.HitDiePools);
        rows.Pairs(0, Self, "spell_notes", f.SpellNotes.Select(p => ((string?)p.Key, (string?)p.Value, (int?)null)));
        rows.Pairs(0, Self, "ammunition_fired", sheet.AmmunitionFired.Select(p => ((string?)p.Key, (string?)null, (int?)p.Value)));
        rows.Names(0, Self, "completed_operation_ids", f.CompletedOperationIds);
        rows.AddCombat(id, 0, f.Combat);
        rows.AddRoll(id, 0, f.LastRoll);
        rows.AddCast(id, 0, f.LastCast);
        foreach (var (entry, i) in f.UndoHistory.Select((x, i) => (x, i)))
        {
            rows.UndoEntries.Add(new() { CharacterId = id, Position = i, OperationId = entry.OperationId, Description = entry.Description, HasAfter = entry.After is not null });
            rows.AddTracked(id, BeforeSlot(i), entry.Before);
            if (entry.After is not null) rows.AddTracked(id, AfterSlot(i), entry.After);
        }
        return rows;
    }

    private static int? Score(Dictionary<Ability, int>? scores, Ability ability) => scores is not null && scores.TryGetValue(ability, out var value) ? value : null;

    private void Present<T>(int slot, string owner, string list, List<T>? values)
    {
        // Lists of structured rows keep "absent" apart from "empty" with a marker row.
        if (values is not null) Lists.Add(new() { CharacterId = Character.Id, Slot = slot, Owner = owner, List = "present:" + list, Position = -1 });
    }

    private void Names(int slot, string owner, string list, IEnumerable<string>? values)
        => Pairs(slot, owner, list, values?.Select(v => ((string?)null, (string?)v, (int?)null)));

    private void Numbers(int slot, string owner, string list, IEnumerable<int>? values)
        => Pairs(slot, owner, list, values?.Select(v => ((string?)null, (string?)null, (int?)v)));

    private void Pairs(int slot, string owner, string list, IEnumerable<(string? Key, string? Value, int? Number)>? values)
    {
        if (values is null) return;
        var position = 0;
        foreach (var (key, value, number) in values)
            Lists.Add(new() { CharacterId = Character.Id, Slot = slot, Owner = owner, List = list, Position = position++, Key = key, Value = value, Number = number });
        if (position == 0) Lists.Add(new() { CharacterId = Character.Id, Slot = slot, Owner = owner, List = list, Position = -1 });
    }

    private void Choice(string owner, Dictionary<string, ChoiceValue>? choices)
    {
        if (choices is null) return;
        ChoiceNode(owner, "", 0, new ChoiceValue { Fields = choices });
    }

    private void ChoiceNode(string owner, string path, int position, ChoiceValue value)
    {
        var row = new ChoiceRow { CharacterId = Character.Id, Owner = owner, Path = path, Position = position };
        if (value.Text is not null) { row.Kind = ChoiceKind.Text; row.Text = value.Text; }
        else if (value.Number is { } number) { row.Kind = ChoiceKind.Number; row.Number = number; }
        else if (value.Flag is { } flag) { row.Kind = ChoiceKind.Flag; row.Flag = flag; }
        else if (value.Items is not null)
        {
            row.Kind = ChoiceKind.List;
            for (var i = 0; i < value.Items.Count; i++) ChoiceNode(owner, path + "/" + i, i, value.Items[i]);
        }
        else if (value.Fields is not null)
        {
            row.Kind = ChoiceKind.Fields;
            var i = 0;
            foreach (var (name, field) in value.Fields) ChoiceNode(owner, path + "/" + Escape(name), i++, field);
        }
        else row.Kind = ChoiceKind.Null;
        Choices.Add(row);
    }

    private static string Escape(string segment) => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    private static string Unescape(string segment) => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private void AddDefenses(string id, string owner, List<DamageDefense> defenses)
    {
        foreach (var (d, i) in defenses.Select((x, i) => (x, i)))
            Defenses.Add(new() { CharacterId = id, Owner = owner, Position = i, Kind = d.Kind, Type = d.Type, Source = d.Source, Note = d.Note });
    }

    private void AddEffects(string id, int slot, string owner, List<ActiveEffect> effects)
    {
        foreach (var (e, i) in effects.Select((x, i) => (x, i)))
            Effects.Add(new() { CharacterId = id, Slot = slot, Owner = owner, Position = i, EffectId = e.Id, Name = e.Name, Source = e.Source, Note = e.Note, RemainingRounds = e.RemainingRounds, Concentration = e.Concentration });
    }

    private void AddResources(string id, int slot, string owner, List<ResourcePool> resources)
    {
        foreach (var (r, i) in resources.Select((x, i) => (x, i)))
            Resources.Add(new() { CharacterId = id, Slot = slot, Owner = owner, Position = i, PoolId = r.Id, SourceId = r.SourceId, SourceKind = r.SourceKind, Name = r.Name,
                Remaining = r.Remaining, Maximum = r.Maximum, Recovery = r.Recovery.ToString(), SlotLevel = r.SlotLevel, ShortRestRecoveryAmount = r.ShortRestRecoveryAmount });
    }

    private void AddHitDiePools(string id, int slot, List<HitDiePool> pools)
    {
        foreach (var (p, i) in pools.Select((x, i) => (x, i)))
        {
            HitDiePools.Add(new() { CharacterId = id, Slot = slot, Position = i, PoolId = p.Id, Sides = p.Sides, Maximum = p.Maximum, Remaining = p.Remaining });
            Names(slot, "pool:" + i, "class_track_ids", p.ClassTrackIds);
        }
    }

    private void AddSpellSources(string id, int slot, List<SpellSourceState> sources)
    {
        foreach (var (s, i) in sources.Select((x, i) => (x, i)))
        {
            var p = s.Preparation;
            SpellSources.Add(new() { CharacterId = id, Slot = slot, Position = i, SourceId = s.Id, ClassTrackId = s.ClassTrackId, SourceKind = s.SourceKind, Name = s.Name,
                Ability = s.Ability.ToString(), MaximumSpellLevel = p.MaximumSpellLevel, CanChangeCantripsOnLongRest = p.CanChangeCantripsOnLongRest,
                CantripReplacementLimit = p.CantripReplacementLimit, CanChangeOnLongRest = p.CanChangeOnLongRest, PreparedLimit = p.PreparedLimit,
                ReplacementLimit = p.ReplacementLimit, FromSpellbook = p.FromSpellbook });
            var owner = "spell_source:" + i;
            Names(slot, owner, "prepared", s.PreparedSpellIds);
            Names(slot, owner, "known", s.KnownSpellIds);
            Names(slot, owner, "spellbook", s.SpellbookIds);
            Names(slot, owner, "granted", s.GrantedSpellIds);
            Names(slot, owner, "owned_cantrips", p.OwnedCantripSpellIds);
            Names(slot, owner, "eligible_cantrips", p.EligibleCantripSpellIds);
            Names(slot, owner, "eligible_spells", p.EligibleSpellIds);
        }
    }

    private void AddCombat(string id, int slot, CombatState c)
    {
        var s = c.AttackSelection;
        var turnStart = c.DeathSavesAtTurnStart;
        Combats.Add(new()
        {
            CharacterId = id, Slot = slot, Version = c.Version, Enabled = c.Enabled, EncounterActive = c.EncounterActive, EncounterId = c.EncounterId, Round = c.Round,
            OwnTurnNumber = c.OwnTurnNumber, Sequence = c.Sequence, Phase = c.Phase.ToString(), TurnId = c.TurnId, ActorId = c.ActorId, PendingOwnTurnBoundaryId = c.PendingOwnTurnBoundaryId,
            ActionAvailable = c.ActionAvailable, BonusActionAvailable = c.BonusActionAvailable, ReactionAvailable = c.ReactionAvailable, AttacksRemaining = c.AttacksRemaining,
            MovementSpent = c.MovementSpent, MovementGranted = c.MovementGranted, SlotCastThisTurn = c.SlotCastThisTurn,
            HasDeathSavesAtTurnStart = turnStart is not null, TurnStartSuccesses = turnStart?.Successes ?? 0, TurnStartFailures = turnStart?.Failures ?? 0,
            TurnStartStable = turnStart?.Stable ?? false, TurnStartDead = turnStart?.Dead ?? false, DeathSaveRecordedTurnId = c.DeathSaveRecordedTurnId, LayoutVersion = c.Layout.Version,
            HasAttackSelection = s is not null, SelectionCastId = s?.CastId ?? "", SelectionRollId = s?.RollId ?? "", SelectionTarget = s?.Target ?? "", SelectionMarkEffectId = s?.MarkEffectId ?? "",
            HasSelectionProfile = s?.AttackProfile is not null, SelectionProfileName = s?.AttackProfile?.Name ?? "", SelectionDiceCount = s?.AttackProfile?.Dice?.Count,
            SelectionDiceSides = s?.AttackProfile?.Dice?.Sides, SelectionModifier = s?.AttackProfile?.Modifier ?? 0, SelectionFixedDamage = s?.AttackProfile?.FixedDamage,
            SelectionDamageType = s?.AttackProfile?.DamageType ?? ""
        });
        Names(slot, "combat", "completed_boundary_ids", c.CompletedBoundaryIds);
        Names(slot, "combat", "layout.sections", c.Layout.Sections);
        Names(slot, "combat", "layout.hidden_sections", c.Layout.HiddenSections);
        Names(slot, "combat", "layout.pinned_skills", c.Layout.PinnedSkills);
        Names(slot, "combat", "layout.pinned_spells", c.Layout.PinnedSpells);
        Names(slot, "combat", "layout.pinned_features", c.Layout.PinnedFeatures);
        Names(slot, "combat", "layout.pinned_items", c.Layout.PinnedItems);
        if (s is not null) Names(slot, "combat", "selection.armed_effects", s.ArmedEffects);
        foreach (var (t, i) in c.Timers.Select((x, i) => (x, i)))
            CombatTimers.Add(new() { CharacterId = id, Slot = slot, Position = i, EffectId = t.EffectId, CreatorId = t.CreatorId, TargetId = t.TargetId, Duration = t.Duration.ToString(),
                OriginalUnit = t.OriginalUnit, OriginalAmount = t.OriginalAmount, Remaining = t.Remaining, CreatedSequence = t.CreatedSequence, AwaitingSave = t.AwaitingSave, SavePrompt = t.SavePrompt });
        foreach (var (g, i) in c.AdditionalActions.Select((x, i) => (x, i)))
        {
            CombatGrants.Add(new() { CharacterId = id, Slot = slot, Position = i, GrantId = g.Id, Source = g.Source, Remaining = g.Remaining });
            Names(slot, "grant:" + i, "permitted_actions", g.PermittedActions);
        }
    }

    private void AddRoll(string id, int slot, RollOutcome? r)
    {
        if (r is null) return;
        Rolls.Add(new() { CharacterId = id, Slot = slot, RollId = r.Id, AttackKind = r.AttackKind.ToString(), BonusDieSides = r.BonusDieSides, InspirationWindowId = r.InspirationWindowId,
            Name = r.Name, Kind = r.Kind.ToString(), Mode = r.Mode.ToString(), Advantage = r.Advantage.ToString(), StandingBonusTotal = r.StandingBonus.Total, FlatBonus = r.FlatBonus,
            ChosenD20 = r.ChosenD20, Total = r.Total, LuckyUsed = r.LuckyUsed, Resolved = r.Resolved, Hit = r.Hit });
        Numbers(slot, "roll", "d20", r.D20);
        Numbers(slot, "roll", "bonus_dice", r.BonusDice);
        Pairs(slot, "roll", "standing_terms", r.StandingBonus.Terms.Select(t => ((string?)t.Label, (string?)null, (int?)t.Value)));
        foreach (var (d, i) in r.Damage.Select((x, i) => (x, i)))
        {
            DamageTerms.Add(new() { CharacterId = id, Slot = slot, Position = i, DieSides = d.DieSides, Name = d.Name, DamageType = d.DamageType, Modifier = d.Modifier, Total = d.Total });
            Numbers(slot, "damage:" + i, "dice", d.Dice);
        }
    }

    private void AddCast(string id, int slot, CastOutcome? c)
    {
        if (c is null) return;
        var p = c.Preview;
        Casts.Add(new() { CharacterId = id, Slot = slot, CastId = c.Id, AttacksRemaining = c.AttacksRemaining, InspirationWindowId = c.InspirationWindowId, Ritual = c.Ritual,
            Mode = c.Mode.ToString(), Target = c.Target, PaymentResourceId = c.PaymentResourceId, EffectTotal = c.EffectTotal,
            PreviewAttackRollKind = p.AttackRollKind, PreviewAttackRollCount = p.AttackRollCount,
            HasCastingModifierCalculation = p.CastingModifierCalculation is not null, CastingModifierTotal = p.CastingModifierCalculation?.Total ?? 0,
            HasSaveDcCalculation = p.SaveDcCalculation is not null, SaveDcTotal = p.SaveDcCalculation?.Total ?? 0,
            SpellId = p.SpellId, SpellName = p.Name, SlotLevel = p.SlotLevel, EffectKind = p.EffectKind.ToString(), DiceCount = p.Dice?.Count, DiceSides = p.Dice?.Sides,
            Modifier = p.Modifier, FormulaText = p.FormulaText, DamageType = p.DamageType, RadiusFt = p.RadiusFt, SaveAbility = p.SaveAbility?.ToString(), SaveDc = p.SaveDc,
            Concentration = p.Concentration, DurationText = p.DurationText, Resolution = p.Resolution, Trigger = p.Trigger, Note = p.Note, Modeled = p.Modeled });
        Numbers(slot, "cast", "effect_dice", c.EffectDice);
        if (p.CastingModifierCalculation is { } modifier) Pairs(slot, "cast", "casting_modifier_terms", modifier.Terms.Select(t => ((string?)t.Label, (string?)null, (int?)t.Value)));
        if (p.SaveDcCalculation is { } dc) Pairs(slot, "cast", "save_dc_terms", dc.Terms.Select(t => ((string?)t.Label, (string?)null, (int?)t.Value)));
    }

    private void AddTracked(string id, int slot, PlayTrackedState t)
    {
        PlaySnapshots.Add(new() { CharacterId = id, Slot = slot, ReceiptsCaptured = t.ReceiptsCaptured, HasCombat = t.Combat is not null, HasSpellSources = t.SpellSources is not null,
            HasHitDiePools = t.HitDiePools is not null, HasDeathSaves = t.DeathSaves is not null, DeathSaveSuccesses = t.DeathSaves?.Successes ?? 0,
            DeathSaveFailures = t.DeathSaves?.Failures ?? 0, Stable = t.DeathSaves?.Stable ?? false, Dead = t.DeathSaves?.Dead ?? false, HeroicInspiration = t.HeroicInspiration,
            CurrentHp = t.CurrentHp, TemporaryHp = t.TemporaryHp, Turn = t.Turn, Exhaustion = t.Exhaustion, DreadUsedThisTurn = t.DreadUsedThisTurn, HunterTarget = t.HunterTarget });
        AddRoll(id, slot, t.LastRoll);
        AddCast(id, slot, t.LastCast);
        if (t.Combat is not null) AddCombat(id, slot, t.Combat);
        if (t.SpellSources is not null) AddSpellSources(id, slot, t.SpellSources);
        if (t.HitDiePools is not null) AddHitDiePools(id, slot, t.HitDiePools);
        Names(slot, Self, "known_spell_ids", t.KnownSpellIds);
        Names(slot, Self, "owned_cantrip_spell_ids", t.OwnedCantripSpellIds);
        Names(slot, Self, "prepared_spell_ids", t.PreparedSpellIds);
        AddResources(id, slot, Self, t.Resources);
        AddEffects(id, slot, Self, t.Effects);
    }

    // ---------- rows -> record ----------

    public CharacterRecord ToRecord()
    {
        var c = Character;
        var lists = Lists.ToLookup(l => (l.Slot, l.Owner, l.List));
        bool Has(int slot, string owner, string list) => lists.Contains((slot, owner, list));
        List<string>? NamesOf(int slot, string owner, string list) => Has(slot, owner, list) ? [.. lists[(slot, owner, list)].Where(l => l.Position >= 0).OrderBy(l => l.Position).Select(l => l.Value ?? "")] : null;
        List<int>? NumbersOf(int slot, string owner, string list) => Has(slot, owner, list) ? [.. lists[(slot, owner, list)].Where(l => l.Position >= 0).OrderBy(l => l.Position).Select(l => l.Number ?? 0)] : null;
        IEnumerable<ListValueRow>? PairsOf(int slot, string owner, string list) => Has(slot, owner, list) ? lists[(slot, owner, list)].Where(l => l.Position >= 0).OrderBy(l => l.Position) : null;
        Dictionary<string, string>? TextMap(int slot, string owner, string list) => PairsOf(slot, owner, list)?.ToDictionary(l => l.Key ?? "", l => l.Value ?? "", StringComparer.Ordinal);
        Dictionary<string, int>? NumberMap(int slot, string owner, string list) => PairsOf(slot, owner, list)?.ToDictionary(l => l.Key ?? "", l => l.Number ?? 0, StringComparer.Ordinal);
        Dictionary<Ability, int>? AbilityMap(int slot, string owner, string list) => PairsOf(slot, owner, list)?.ToDictionary(l => Enum.Parse<Ability>(l.Key!), l => l.Number ?? 0);
        List<CalculationTerm> Terms(int slot, string owner, string list) => [.. PairsOf(slot, owner, list)?.Select(l => new CalculationTerm(l.Key ?? "", l.Number ?? 0)) ?? []];
        bool Present(string list) => Has(0, Self, "present:" + list);
        var choices = Choices.ToLookup(x => x.Owner);
        Dictionary<string, ChoiceValue>? ChoiceMap(string owner) => choices.Contains(owner) ? ReadChoice(choices[owner]).Fields : null;
        ChoiceValue? ChoiceOf(string owner) => choices.Contains(owner) ? ReadChoice(choices[owner]) : null;

        Dictionary<Ability, int>? scores = null;
        if (c.BaseStrength is not null || c.BaseDexterity is not null || c.BaseConstitution is not null || c.BaseIntelligence is not null || c.BaseWisdom is not null || c.BaseCharisma is not null)
        {
            scores = [];
            void Put(Ability a, int? v) { if (v is { } value) scores[a] = value; }
            Put(Ability.Strength, c.BaseStrength); Put(Ability.Dexterity, c.BaseDexterity); Put(Ability.Constitution, c.BaseConstitution);
            Put(Ability.Intelligence, c.BaseIntelligence); Put(Ability.Wisdom, c.BaseWisdom); Put(Ability.Charisma, c.BaseCharisma);
        }

        var build = new BuildFacts
        {
            Name = c.Name, ClassName = c.ClassName, Subclass = c.Subclass, Level = c.Level, Species = c.Species, Background = c.Background,
            ClassLevels = NumberMap(0, Self, "class_levels"),
            ClassOptions = ChoiceMap("class_options"), SpeciesOptions = ChoiceMap("species_options"), BackgroundOptions = ChoiceMap("background_options"),
            OriginFeatChoices = ChoiceMap("origin_feat_choices"), HeroholdOriginFeatChoices = ChoiceMap("folio_origin_feat_choices"),
            OriginChoiceOwners = ChoiceMap("folio_origin_choice_owners"), PdfPassthroughFields = ChoiceMap("pdf_passthrough_fields"),
            ImportedMovementModes = ChoiceMap("movement_modes"), ImportedCoins = ChoiceOf("imported:coins"), ImportedCurrency = ChoiceOf("imported:currency"),
            ImportedExperiencePoints = ChoiceOf("imported:experience_points"), ImportedXp = ChoiceOf("imported:xp"), ImportedHeroicInspiration = ChoiceOf("imported:heroic_inspiration"),
            BaseAbilityScores = scores, AbilityScoreMethod = c.AbilityScoreMethod, AbilityScoreAssignments = NumberMap(0, Self, "ability_score_assignments"),
            RolledAbilityScores = NumbersOf(0, Self, "rolled_ability_scores"),
            AbilityScoreAdjustments = Present("ability_score_adjustments") ? [.. AbilityAdjustments.OrderBy(a => a.Position).Select(a => new AbilityScoreAdjustmentFact
            {
                Type = a.Type, SourceType = a.SourceType, SourceId = a.SourceId, InstanceId = a.InstanceId, SourceFeat = a.SourceFeat, SourceName = a.SourceName, SourceClass = a.SourceClass, SourceLevel = a.SourceLevel,
                Source = a.HasSource ? new BuildSourceFact { Type = a.SourceRefType ?? "", ClassName = a.SourceRefClassName, Level = a.SourceRefLevel, TransactionId = a.SourceRefTransactionId } : null,
                LevelUpChoiceId = a.LevelUpChoiceId, FeatInstanceId = a.FeatInstanceId, Adjustments = AbilityMap(0, "adjustment:" + a.Position, "amounts") ?? [], Maximum = a.Maximum
            })] : null,
            Feats = Present("general_feats") ? [.. Feats.OrderBy(x => x.Position).Select(x => new FeatFact
            {
                InstanceId = x.InstanceId, FeatId = x.FeatId, Name = x.Name, Repeatable = x.Repeatable,
                Source = new() { Type = x.SourceType, ClassName = x.SourceClassName, Level = x.SourceLevel, TransactionId = x.SourceTransactionId },
                Choices = ChoiceMap("feat:" + x.Position) ?? new(StringComparer.Ordinal), LevelTaken = x.LevelTaken, AbilityIncreaseInBase = x.AbilityIncreaseInBase,
                LegacyChoiceHistory = ChoiceOf("feat:" + x.Position + ":history")?.Items?.Select(item => item.Fields ?? new(StringComparer.Ordinal)).ToList()
            })] : null,
            OriginLanguages = NamesOf(0, Self, "origin_languages"),
            Spells = c.HasSpellPicks ? new SpellPicks { Cantrips = NamesOf(0, Self, "spells.cantrips"), Known = NamesOf(0, Self, "spells.known"), Prepared = NamesOf(0, Self, "spells.prepared"), Spellbook = NamesOf(0, Self, "spells.spellbook") } : null,
            SpellbookSpells = NamesOf(0, Self, "spellbook_spells"), WeaponMasteries = NamesOf(0, Self, "weapon_masteries"), EquipmentMigrationPending = c.EquipmentMigrationPending,
            Presentation = c.HasPresentation ? new PresentationFacts { PrimaryWeapon = c.PrimaryWeapon, FeaturedCantrip = c.FeaturedCantrip, WeaponWieldingModes = TextMap(0, Self, "weapon_wielding_modes") ?? new(StringComparer.Ordinal) } : null,
            FeatureConfigurations = Present("feature_configurations") ? [.. FeatureConfigurations.OrderBy(x => x.Position).Select(x => new FeatureConfigurationFact { FeatureId = x.FeatureId, CurrentValue = x.CurrentValue })] : null,
            HpRolls = Present("hp_rolls") ? [.. HpRolls.OrderBy(x => x.Position).Select(x => new HpRollFact { Level = x.Level, Value = x.Value, Mode = x.Mode })] : null,
            HpHistory = Present("hp_history") ? [.. HpGains.OrderBy(x => x.Position).Select(x => new HpGainFact { CharacterLevel = x.CharacterLevel, ClassName = x.ClassName, Method = x.Method, BaseGain = x.BaseGain })] : null,
            LevelHistory = Present("level_history") ? [.. LevelUps.OrderBy(x => x.Position).Select(x => new LevelUpFact
            {
                ClassName = x.ClassName, FromLevel = x.FromLevel, ToLevel = x.ToLevel, TransactionId = x.TransactionId, HpIncrease = x.HpIncrease, Source = x.Source,
                AutomaticFeatures = NamesOf(0, "level_up:" + x.Position, "automatic_features") ?? [],
                ResolvedChoices = [.. ResolvedChoices.Where(r => r.LevelUpPosition == x.Position).OrderBy(r => r.Position)
                    .Select(r => new ResolvedChoiceFact { Id = r.ChoiceId, Value = choices.Contains("level_up:" + x.Position + ":" + r.Position) ? ReadChoice(choices["level_up:" + x.Position + ":" + r.Position]) : new() })]
            })] : null,
            CompletedLevelUpTransactions = NamesOf(0, Self, "completed_level_up_transactions"),
            ImportOverrides = c.ImportMaximumHpAdjustment is { } adjustment ? new ImportOverrideFacts { MaximumHpAdjustment = adjustment } : null,
            ImportProvenance = c.HasImportProvenance ? new ImportProvenanceFacts { Warnings = NamesOf(0, Self, "import_warnings") ?? [], SourceKind = c.ImportSourceKind, Status = c.ImportStatus,
                ImportedAt = c.ImportedAt, AbilityHistoryReconstructed = c.ImportAbilityHistoryReconstructed, HpHistoryReconstructed = c.ImportHpHistoryReconstructed } : null,
            MigrationWarnings = NamesOf(0, Self, "migration_warnings"),
            ClassTracks = Present("folio_class_tracks") ? [.. ClassTracks.OrderBy(x => x.Position).Select(x => new ClassTrackFact { Id = x.TrackId, ClassName = x.ClassName, Level = x.Level, RulesSource = x.RulesSource, Subclass = x.Subclass, Choices = ChoiceMap("track:" + x.Position) ?? new(StringComparer.Ordinal) })] : null,
            ClassTracksVersion = c.ClassTracksVersion, InitialClassTrackId = c.InitialClassTrackId,
            Journal = c.JournalVersion is { } journalVersion ? new JournalCategoryFacts { Version = journalVersion, Categories = TextMap(0, Self, "journal_categories") ?? new(StringComparer.Ordinal) } : null,
            CustomJournal = c.CustomJournalVersion is { } customVersion ? new CustomJournalFacts { Version = customVersion, Categories = NamesOf(0, Self, "custom_journal_categories") ?? [], Notes = TextMap(0, Self, "custom_journal_notes") ?? new(StringComparer.Ordinal) } : null,
            ArchivedJournalNotes = NamesOf(0, Self, "archived_journal_notes"), JournalCleanupV1 = c.JournalCleanupV1,
            ManualFacts = c.HasManualFacts ? new ManualBuildFacts
            {
                ToolProficiencies = NamesOf(0, Self, "manual.tool_proficiencies") ?? [], ArmorProficiencies = NamesOf(0, Self, "manual.armor_proficiencies") ?? [],
                WeaponProficiencies = NamesOf(0, Self, "manual.weapon_proficiencies") ?? [], DamageResistances = NamesOf(0, Self, "manual.damage_resistances") ?? [],
                DamageImmunities = NamesOf(0, Self, "manual.damage_immunities") ?? [], DamageVulnerabilities = NamesOf(0, Self, "manual.damage_vulnerabilities") ?? [],
                ConditionImmunities = NamesOf(0, Self, "manual.condition_immunities") ?? []
            } : null,
            ManualLanguages = NamesOf(0, Self, "manual.languages"), ManualSkills = NamesOf(0, Self, "manual.skills"),
            ManualExpertise = NamesOf(0, Self, "manual.expertise"), ManualSaves = NamesOf(0, Self, "manual.saves")
        };

        List<DamageDefense> DefensesOf(string owner) => [.. Defenses.Where(d => d.Owner == owner).OrderBy(d => d.Position).Select(d => new DamageDefense { Kind = d.Kind, Type = d.Type, Source = d.Source, Note = d.Note })];
        List<ActiveEffect> EffectsOf(int slot, string owner) => [.. Effects.Where(e => e.Slot == slot && e.Owner == owner).OrderBy(e => e.Position)
            .Select(e => new ActiveEffect { Id = e.EffectId, Name = e.Name, Source = e.Source, Note = e.Note, RemainingRounds = e.RemainingRounds, Concentration = e.Concentration })];
        List<ResourcePool> ResourcesOf(int slot, string owner) => [.. Resources.Where(r => r.Slot == slot && r.Owner == owner).OrderBy(r => r.Position)
            .Select(r => new ResourcePool { Id = r.PoolId, SourceId = r.SourceId, SourceKind = r.SourceKind, Name = r.Name, Remaining = r.Remaining, Maximum = r.Maximum,
                Recovery = Enum.Parse<RecoveryRest>(r.Recovery), SlotLevel = r.SlotLevel, ShortRestRecoveryAmount = r.ShortRestRecoveryAmount })];
        List<HitDiePool> PoolsOf(int slot) => [.. HitDiePools.Where(p => p.Slot == slot).OrderBy(p => p.Position)
            .Select(p => new HitDiePool { Id = p.PoolId, Sides = p.Sides, Maximum = p.Maximum, Remaining = p.Remaining, ClassTrackIds = NamesOf(slot, "pool:" + p.Position, "class_track_ids") ?? [] })];
        List<SpellSourceState> SourcesOf(int slot) => [.. SpellSources.Where(s => s.Slot == slot).OrderBy(s => s.Position).Select(s =>
        {
            var owner = "spell_source:" + s.Position;
            return new SpellSourceState
            {
                Id = s.SourceId, ClassTrackId = s.ClassTrackId, SourceKind = s.SourceKind, Name = s.Name, Ability = Enum.Parse<Ability>(s.Ability),
                Preparation = new PreparationPolicy
                {
                    MaximumSpellLevel = s.MaximumSpellLevel, CanChangeCantripsOnLongRest = s.CanChangeCantripsOnLongRest, CantripReplacementLimit = s.CantripReplacementLimit,
                    OwnedCantripSpellIds = NamesOf(slot, owner, "owned_cantrips") ?? [], EligibleCantripSpellIds = NamesOf(slot, owner, "eligible_cantrips") ?? [],
                    CanChangeOnLongRest = s.CanChangeOnLongRest, PreparedLimit = s.PreparedLimit, ReplacementLimit = s.ReplacementLimit, FromSpellbook = s.FromSpellbook,
                    EligibleSpellIds = NamesOf(slot, owner, "eligible_spells") ?? []
                },
                PreparedSpellIds = NamesOf(slot, owner, "prepared") ?? [], KnownSpellIds = NamesOf(slot, owner, "known") ?? [],
                SpellbookIds = NamesOf(slot, owner, "spellbook") ?? [], GrantedSpellIds = NamesOf(slot, owner, "granted") ?? []
            };
        })];
        CombatState? CombatOf(int slot)
        {
            var r = Combats.SingleOrDefault(x => x.Slot == slot);
            if (r is null) return null;
            var defaults = new CombatLayout();
            return new CombatState
            {
                Version = r.Version, Enabled = r.Enabled, EncounterActive = r.EncounterActive, EncounterId = r.EncounterId, Round = r.Round, OwnTurnNumber = r.OwnTurnNumber,
                Sequence = r.Sequence, Phase = Enum.Parse<CombatPhase>(r.Phase), TurnId = r.TurnId, ActorId = r.ActorId, PendingOwnTurnBoundaryId = r.PendingOwnTurnBoundaryId,
                CompletedBoundaryIds = NamesOf(slot, "combat", "completed_boundary_ids") ?? [], ActionAvailable = r.ActionAvailable, BonusActionAvailable = r.BonusActionAvailable,
                ReactionAvailable = r.ReactionAvailable, AttacksRemaining = r.AttacksRemaining, MovementSpent = r.MovementSpent, MovementGranted = r.MovementGranted,
                SlotCastThisTurn = r.SlotCastThisTurn,
                DeathSavesAtTurnStart = r.HasDeathSavesAtTurnStart ? new DeathSaveState { Successes = r.TurnStartSuccesses, Failures = r.TurnStartFailures, Stable = r.TurnStartStable, Dead = r.TurnStartDead } : null,
                DeathSaveRecordedTurnId = r.DeathSaveRecordedTurnId,
                AdditionalActions = [.. CombatGrants.Where(g => g.Slot == slot).OrderBy(g => g.Position).Select(g => new CombatActionGrant { Id = g.GrantId, Source = g.Source, Remaining = g.Remaining, PermittedActions = NamesOf(slot, "grant:" + g.Position, "permitted_actions") ?? [] })],
                Timers = [.. CombatTimers.Where(t => t.Slot == slot).OrderBy(t => t.Position).Select(t => new CombatEffectTimer
                {
                    EffectId = t.EffectId, CreatorId = t.CreatorId, TargetId = t.TargetId, Duration = Enum.Parse<CombatDuration>(t.Duration), OriginalUnit = t.OriginalUnit,
                    OriginalAmount = t.OriginalAmount, Remaining = t.Remaining, CreatedSequence = t.CreatedSequence, AwaitingSave = t.AwaitingSave, SavePrompt = t.SavePrompt
                })],
                AttackSelection = r.HasAttackSelection ? new CombatAttackSelection
                {
                    CastId = r.SelectionCastId, RollId = r.SelectionRollId, Target = r.SelectionTarget, MarkEffectId = r.SelectionMarkEffectId,
                    ArmedEffects = NamesOf(slot, "combat", "selection.armed_effects") ?? [],
                    AttackProfile = r.HasSelectionProfile ? new CombatAttackProfile
                    {
                        Name = r.SelectionProfileName, Dice = r.SelectionDiceCount is { } count && r.SelectionDiceSides is { } sides ? new DiceFormula(count, sides) : null,
                        Modifier = r.SelectionModifier, FixedDamage = r.SelectionFixedDamage, DamageType = r.SelectionDamageType
                    } : null
                } : null,
                Layout = new CombatLayout
                {
                    Version = r.LayoutVersion, Sections = NamesOf(slot, "combat", "layout.sections") ?? defaults.Sections,
                    HiddenSections = NamesOf(slot, "combat", "layout.hidden_sections") ?? defaults.HiddenSections, PinnedSkills = NamesOf(slot, "combat", "layout.pinned_skills") ?? defaults.PinnedSkills,
                    PinnedSpells = NamesOf(slot, "combat", "layout.pinned_spells") ?? defaults.PinnedSpells, PinnedFeatures = NamesOf(slot, "combat", "layout.pinned_features") ?? defaults.PinnedFeatures,
                    PinnedItems = NamesOf(slot, "combat", "layout.pinned_items") ?? defaults.PinnedItems
                }
            };
        }
        RollOutcome? RollOf(int slot)
        {
            var r = Rolls.SingleOrDefault(x => x.Slot == slot);
            if (r is null) return null;
            return new RollOutcome
            {
                Id = r.RollId, AttackKind = Enum.Parse<AttackKind>(r.AttackKind), BonusDieSides = r.BonusDieSides, InspirationWindowId = r.InspirationWindowId, Name = r.Name,
                Kind = Enum.Parse<RollKind>(r.Kind), Mode = Enum.Parse<DiceMode>(r.Mode), Advantage = Enum.Parse<RollAdvantage>(r.Advantage),
                D20 = NumbersOf(slot, "roll", "d20") ?? [], BonusDice = NumbersOf(slot, "roll", "bonus_dice") ?? [],
                StandingBonus = new(r.StandingBonusTotal, Terms(slot, "roll", "standing_terms")), FlatBonus = r.FlatBonus, ChosenD20 = r.ChosenD20, Total = r.Total,
                LuckyUsed = r.LuckyUsed, Resolved = r.Resolved, Hit = r.Hit,
                Damage = [.. DamageTerms.Where(d => d.Slot == slot).OrderBy(d => d.Position).Select(d => new DamageTerm
                    { DieSides = d.DieSides, Name = d.Name, DamageType = d.DamageType, Dice = NumbersOf(slot, "damage:" + d.Position, "dice") ?? [], Modifier = d.Modifier, Total = d.Total })]
            };
        }
        CastOutcome? CastOf(int slot)
        {
            var r = Casts.SingleOrDefault(x => x.Slot == slot);
            if (r is null) return null;
            return new CastOutcome
            {
                Id = r.CastId, AttacksRemaining = r.AttacksRemaining, InspirationWindowId = r.InspirationWindowId, Ritual = r.Ritual, Mode = Enum.Parse<DiceMode>(r.Mode),
                Target = r.Target, PaymentResourceId = r.PaymentResourceId, EffectDice = NumbersOf(slot, "cast", "effect_dice") ?? [], EffectTotal = r.EffectTotal,
                Preview = new CastingPreview
                {
                    AttackRollKind = r.PreviewAttackRollKind, AttackRollCount = r.PreviewAttackRollCount,
                    CastingModifierCalculation = r.HasCastingModifierCalculation ? new(r.CastingModifierTotal, Terms(slot, "cast", "casting_modifier_terms")) : null,
                    SaveDcCalculation = r.HasSaveDcCalculation ? new(r.SaveDcTotal, Terms(slot, "cast", "save_dc_terms")) : null,
                    SpellId = r.SpellId, Name = r.SpellName, SlotLevel = r.SlotLevel, EffectKind = Enum.Parse<SpellEffectKind>(r.EffectKind),
                    Dice = r.DiceCount is { } count && r.DiceSides is { } sides ? new DiceFormula(count, sides) : null, Modifier = r.Modifier, FormulaText = r.FormulaText,
                    DamageType = r.DamageType, RadiusFt = r.RadiusFt, SaveAbility = r.SaveAbility is null ? null : Enum.Parse<Ability>(r.SaveAbility), SaveDc = r.SaveDc,
                    Concentration = r.Concentration, DurationText = r.DurationText, Resolution = r.Resolution, Trigger = r.Trigger, Note = r.Note, Modeled = r.Modeled
                }
            };
        }
        PlayTrackedState TrackedOf(int slot)
        {
            var s = PlaySnapshots.Single(x => x.Slot == slot);
            return new PlayTrackedState
            {
                ReceiptsCaptured = s.ReceiptsCaptured, LastRoll = RollOf(slot), LastCast = CastOf(slot), Combat = s.HasCombat ? CombatOf(slot) : null,
                SpellSources = s.HasSpellSources ? SourcesOf(slot) : null, HitDiePools = s.HasHitDiePools ? PoolsOf(slot) : null,
                KnownSpellIds = NamesOf(slot, Self, "known_spell_ids"), OwnedCantripSpellIds = NamesOf(slot, Self, "owned_cantrip_spell_ids"),
                DeathSaves = s.HasDeathSaves ? new DeathSaveState { Successes = s.DeathSaveSuccesses, Failures = s.DeathSaveFailures, Stable = s.Stable, Dead = s.Dead } : null,
                HeroicInspiration = s.HeroicInspiration, CurrentHp = s.CurrentHp, TemporaryHp = s.TemporaryHp, Turn = s.Turn, Exhaustion = s.Exhaustion,
                Resources = ResourcesOf(slot, Self), Effects = EffectsOf(slot, Self), PreparedSpellIds = NamesOf(slot, Self, "prepared_spell_ids") ?? [],
                DreadUsedThisTurn = s.DreadUsedThisTurn, HunterTarget = s.HunterTarget
            };
        }

        var facts = new CharacterFacts
        {
            CurrentHp = c.CurrentHp, TemporaryHp = c.TemporaryHp, Turn = c.Turn, Exhaustion = c.Exhaustion,
            Equipment = [.. Items.OrderBy(i => i.Position).Select(i => (Catalog.GetValueOrDefault(i.CatalogItemId) ?? throw new InvalidDataException("A stored item's catalog item is missing."))
                .Owned(new EquipmentItem { Id = i.ItemId, Quantity = i.Quantity, Equipped = i.Equipped, Attuned = i.Attuned, Carried = i.Carried,
                    Charges = new ItemCharges { Remaining = i.ChargeRemaining }, ScrollSpell = i.ScrollSpell }))],
            Notes = [.. JournalNotes.OrderBy(n => n.Position).Select(n => new JournalNote { Id = n.NoteId, Title = n.Title, Text = n.Text, CreatedAt = n.CreatedAt })],
            Effects = EffectsOf(0, Self),
            Combat = CombatOf(0) ?? new(),
            Sheet = new CharacterSheetState
            {
                Adventure = new AdventureFacts
                {
                    Movement = [.. MovementModes.OrderBy(m => m.Position).Select(m => new MovementMode { Kind = m.Kind, Speed = m.Speed, Source = m.Source, Note = m.Note })],
                    Companions = [.. Companions.OrderBy(x => x.Position).Select(x => new CompanionSheet
                    {
                        Id = x.CompanionId, Name = x.Name, Kind = x.Kind, ArmorClass = x.ArmorClass, MaximumHp = x.MaximumHp, CurrentHp = x.CurrentHp, TemporaryHp = x.TemporaryHp,
                        Speed = x.Speed, Source = x.Source, Notes = x.Notes,
                        Actions = [.. CompanionActions.Where(a => a.CompanionPosition == x.Position).OrderBy(a => a.Position).Select(a => new CompanionAction { Name = a.Name, Rules = a.Rules })],
                        Resources = ResourcesOf(0, "companion:" + x.Position), Conditions = NamesOf(0, "companion:" + x.Position, "conditions") ?? []
                    })],
                    CapacityOverrideLb = c.CapacityOverrideLb, CapacitySource = c.CapacitySource, CarryCoins = c.CarryCoins
                },
                Profile = new CharacterProfile
                {
                    Background = c.ProfileBackground, Size = c.ProfileSize, Appearance = c.Appearance, Backstory = c.Backstory, Personality = c.Personality, Alignment = c.Alignment,
                    PlayerName = c.PlayerName, Age = c.Age, Height = c.Height, Weight = c.Weight, Eyes = c.Eyes, Skin = c.Skin, Hair = c.Hair, Ideals = c.Ideals, Bonds = c.Bonds,
                    Flaws = c.Flaws, Allies = c.Allies, Organizations = c.Organizations, Treasure = c.Treasure,
                    Languages = NamesOf(0, Self, "profile.languages"), ArmorTraining = NamesOf(0, Self, "profile.armor_training"),
                    WeaponProficiencies = NamesOf(0, Self, "profile.weapon_proficiencies"), ToolProficiencies = NamesOf(0, Self, "profile.tool_proficiencies")
                },
                Coins = c.HasCoins ? new CoinPurse { Cp = c.CoinsCp, Sp = c.CoinsSp, Ep = c.CoinsEp, Gp = c.CoinsGp, Pp = c.CoinsPp } : null,
                ExperiencePoints = c.ExperiencePoints, HeroicInspiration = c.HeroicInspiration,
                DeathSaves = new DeathSaveState { Successes = c.DeathSaveSuccesses, Failures = c.DeathSaveFailures, Stable = c.Stable, Dead = c.Dead },
                CountAmmunition = c.CountAmmunition, AmmunitionFired = NumberMap(0, Self, "ammunition_fired") ?? []
            },
            KnownTarget = new KnownTargetState { Name = c.KnownTargetName, Knowledge = c.KnownTargetKnowledge, Defenses = DefensesOf("target") },
            Defenses = DefensesOf(Self),
            Resources = ResourcesOf(0, Self),
            HitDiePools = PoolsOf(0),
            SpellNotes = TextMap(0, Self, "spell_notes") ?? new(StringComparer.Ordinal),
            DreadUsedThisTurn = c.DreadUsedThisTurn, HunterTarget = c.HunterTarget,
            LastRoll = RollOf(0), LastCast = CastOf(0),
            CompletedOperationIds = NamesOf(0, Self, "completed_operation_ids") ?? [],
            UndoHistory = [.. UndoEntries.OrderBy(u => u.Position).Select(u => new PlayUndoEntry
                { OperationId = u.OperationId, Description = u.Description, Before = TrackedOf(BeforeSlot(u.Position)), After = u.HasAfter ? TrackedOf(AfterSlot(u.Position)) : null })]
        };
        return new CharacterRecord { Id = c.Id, OwnerId = c.OwnerId, RulesSource = c.RulesSource, Revision = c.Revision, Build = build, Facts = facts };
    }

    private static ChoiceValue ReadChoice(IEnumerable<ChoiceRow> rows)
    {
        var byPath = rows.ToDictionary(r => r.Path, StringComparer.Ordinal);
        var children = rows.Where(r => r.Path.Length > 0).GroupBy(r => r.Path[..r.Path.LastIndexOf('/')], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Position).ToList(), StringComparer.Ordinal);
        ChoiceValue Node(ChoiceRow row) => row.Kind switch
        {
            ChoiceKind.Text => new() { Text = row.Text ?? "" },
            ChoiceKind.Number => new() { Number = row.Number },
            ChoiceKind.Flag => new() { Flag = row.Flag },
            ChoiceKind.List => new() { Items = [.. children.GetValueOrDefault(row.Path, []).Select(Node)] },
            ChoiceKind.Fields => new() { Fields = children.GetValueOrDefault(row.Path, []).ToDictionary(child => Unescape(child.Path[(child.Path.LastIndexOf('/') + 1)..]), Node, StringComparer.Ordinal) },
            _ => new()
        };
        return Node(byPath[""]);
    }
}
