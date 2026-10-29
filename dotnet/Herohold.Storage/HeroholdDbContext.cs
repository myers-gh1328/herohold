using Microsoft.EntityFrameworkCore;

namespace Herohold.Storage;

public sealed class HeroholdDbContext(DbContextOptions<HeroholdDbContext> options) : DbContext(options)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<CharacterRow> Characters => Set<CharacterRow>();
    public DbSet<ListValueRow> ListValues => Set<ListValueRow>();
    public DbSet<ChoiceRow> Choices => Set<ChoiceRow>();
    public DbSet<FeatRow> Feats => Set<FeatRow>();
    public DbSet<AbilityAdjustmentRow> AbilityAdjustments => Set<AbilityAdjustmentRow>();
    public DbSet<FeatureConfigurationRow> FeatureConfigurations => Set<FeatureConfigurationRow>();
    public DbSet<HpGainRow> HpGains => Set<HpGainRow>();
    public DbSet<HpRollRow> HpRolls => Set<HpRollRow>();
    public DbSet<LevelUpRow> LevelUps => Set<LevelUpRow>();
    public DbSet<ResolvedChoiceRow> ResolvedChoices => Set<ResolvedChoiceRow>();
    public DbSet<ClassTrackRow> ClassTracks => Set<ClassTrackRow>();
    public DbSet<ItemRow> Items => Set<ItemRow>();
    public DbSet<DefenseRow> Defenses => Set<DefenseRow>();
    public DbSet<EffectRow> Effects => Set<EffectRow>();
    public DbSet<JournalNoteRow> JournalNotes => Set<JournalNoteRow>();
    public DbSet<MovementModeRow> MovementModes => Set<MovementModeRow>();
    public DbSet<CompanionRow> Companions => Set<CompanionRow>();
    public DbSet<CompanionActionRow> CompanionActions => Set<CompanionActionRow>();
    public DbSet<ResourceRow> Resources => Set<ResourceRow>();
    public DbSet<HitDiePoolRow> HitDiePools => Set<HitDiePoolRow>();
    public DbSet<SpellSourceRow> SpellSources => Set<SpellSourceRow>();
    public DbSet<CombatRow> Combats => Set<CombatRow>();
    public DbSet<CombatTimerRow> CombatTimers => Set<CombatTimerRow>();
    public DbSet<CombatGrantRow> CombatGrants => Set<CombatGrantRow>();
    public DbSet<RollRow> Rolls => Set<RollRow>();
    public DbSet<DamageTermRow> DamageTerms => Set<DamageTermRow>();
    public DbSet<CastRow> Casts => Set<CastRow>();
    public DbSet<PlaySnapshotRow> PlaySnapshots => Set<PlaySnapshotRow>();
    public DbSet<UndoEntryRow> UndoEntries => Set<UndoEntryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserRow>(e => { e.ToTable("users"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(64); });
        model.Entity<CharacterRow>(e =>
        {
            e.ToTable("characters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OwnerId);
        });
        Child<ListValueRow>(model, "character_list_values", e => e.HasIndex(x => new { x.CharacterId, x.Slot, x.Owner, x.List, x.Position }).IsUnique());
        Child<ChoiceRow>(model, "character_choices", e => e.HasIndex(x => new { x.CharacterId, x.Owner, x.Path }).IsUnique());
        Child<FeatRow>(model, "character_feats");
        Child<AbilityAdjustmentRow>(model, "character_ability_adjustments");
        Child<FeatureConfigurationRow>(model, "character_feature_configurations");
        Child<HpGainRow>(model, "character_hp_gains");
        Child<HpRollRow>(model, "character_hp_rolls");
        Child<LevelUpRow>(model, "character_level_ups");
        Child<ResolvedChoiceRow>(model, "character_level_up_choices");
        Child<ClassTrackRow>(model, "character_class_tracks");
        Child<ItemRow>(model, "character_items");
        Child<DefenseRow>(model, "character_defenses");
        Child<EffectRow>(model, "character_effects");
        Child<JournalNoteRow>(model, "character_journal_notes");
        Child<MovementModeRow>(model, "character_movement_modes");
        Child<CompanionRow>(model, "character_companions");
        Child<CompanionActionRow>(model, "character_companion_actions");
        Child<ResourceRow>(model, "character_resources");
        Child<HitDiePoolRow>(model, "character_hit_die_pools");
        Child<SpellSourceRow>(model, "character_spell_sources");
        Child<CombatRow>(model, "character_combat", e => e.HasIndex(x => new { x.CharacterId, x.Slot }).IsUnique());
        Child<CombatTimerRow>(model, "character_combat_timers");
        Child<CombatGrantRow>(model, "character_combat_grants");
        Child<RollRow>(model, "character_rolls", e => e.HasIndex(x => new { x.CharacterId, x.Slot }).IsUnique());
        Child<DamageTermRow>(model, "character_roll_damage");
        Child<CastRow>(model, "character_casts", e => e.HasIndex(x => new { x.CharacterId, x.Slot }).IsUnique());
        Child<PlaySnapshotRow>(model, "character_play_snapshots", e => e.HasIndex(x => new { x.CharacterId, x.Slot }).IsUnique());
        Child<UndoEntryRow>(model, "character_undo_entries");
    }

    private static void Child<T>(ModelBuilder model, string table, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>>? configure = null) where T : class
    {
        model.Entity<T>(e =>
        {
            e.ToTable(table);
            e.HasKey("Id");
            e.HasOne<CharacterRow>().WithMany().HasForeignKey("CharacterId").OnDelete(DeleteBehavior.Cascade);
            if (configure is null) e.HasIndex("CharacterId");
            else configure(e);
        });
    }
}
