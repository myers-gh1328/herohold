using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herohold.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", nullable: false),
                    Subclass = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Species = table.Column<string>(type: "TEXT", nullable: false),
                    Background = table.Column<string>(type: "TEXT", nullable: false),
                    AbilityScoreMethod = table.Column<string>(type: "TEXT", nullable: true),
                    BaseStrength = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseDexterity = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseConstitution = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseIntelligence = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseWisdom = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseCharisma = table.Column<int>(type: "INTEGER", nullable: true),
                    EquipmentMigrationPending = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasPresentation = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrimaryWeapon = table.Column<string>(type: "TEXT", nullable: false),
                    FeaturedCantrip = table.Column<string>(type: "TEXT", nullable: false),
                    ImportMaximumHpAdjustment = table.Column<int>(type: "INTEGER", nullable: true),
                    HasImportProvenance = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportSourceKind = table.Column<string>(type: "TEXT", nullable: true),
                    ImportStatus = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<string>(type: "TEXT", nullable: true),
                    ImportAbilityHistoryReconstructed = table.Column<bool>(type: "INTEGER", nullable: true),
                    ImportHpHistoryReconstructed = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasSpellPicks = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClassTracksVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    InitialClassTrackId = table.Column<string>(type: "TEXT", nullable: true),
                    JournalVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomJournalVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    JournalCleanupV1 = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasManualFacts = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentHp = table.Column<int>(type: "INTEGER", nullable: false),
                    TemporaryHp = table.Column<int>(type: "INTEGER", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Exhaustion = table.Column<int>(type: "INTEGER", nullable: false),
                    DreadUsedThisTurn = table.Column<bool>(type: "INTEGER", nullable: false),
                    HunterTarget = table.Column<string>(type: "TEXT", nullable: false),
                    KnownTargetName = table.Column<string>(type: "TEXT", nullable: false),
                    KnownTargetKnowledge = table.Column<string>(type: "TEXT", nullable: false),
                    ExperiencePoints = table.Column<int>(type: "INTEGER", nullable: true),
                    HeroicInspiration = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasCoins = table.Column<bool>(type: "INTEGER", nullable: false),
                    CoinsCp = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinsSp = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinsEp = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinsGp = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinsPp = table.Column<int>(type: "INTEGER", nullable: false),
                    DeathSaveSuccesses = table.Column<int>(type: "INTEGER", nullable: false),
                    DeathSaveFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    Stable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Dead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapacityOverrideLb = table.Column<decimal>(type: "TEXT", nullable: true),
                    CapacitySource = table.Column<string>(type: "TEXT", nullable: false),
                    CarryCoins = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileBackground = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileSize = table.Column<string>(type: "TEXT", nullable: true),
                    Appearance = table.Column<string>(type: "TEXT", nullable: true),
                    Backstory = table.Column<string>(type: "TEXT", nullable: true),
                    Personality = table.Column<string>(type: "TEXT", nullable: true),
                    Alignment = table.Column<string>(type: "TEXT", nullable: true),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: true),
                    Age = table.Column<string>(type: "TEXT", nullable: true),
                    Height = table.Column<string>(type: "TEXT", nullable: true),
                    Weight = table.Column<string>(type: "TEXT", nullable: true),
                    Eyes = table.Column<string>(type: "TEXT", nullable: true),
                    Skin = table.Column<string>(type: "TEXT", nullable: true),
                    Hair = table.Column<string>(type: "TEXT", nullable: true),
                    Ideals = table.Column<string>(type: "TEXT", nullable: true),
                    Bonds = table.Column<string>(type: "TEXT", nullable: true),
                    Flaws = table.Column<string>(type: "TEXT", nullable: true),
                    Allies = table.Column<string>(type: "TEXT", nullable: true),
                    Organizations = table.Column<string>(type: "TEXT", nullable: true),
                    Treasure = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_characters_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_ability_adjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    InstanceId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceFeat = table.Column<string>(type: "TEXT", nullable: true),
                    SourceName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceClass = table.Column<string>(type: "TEXT", nullable: true),
                    SourceLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    HasSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceRefType = table.Column<string>(type: "TEXT", nullable: true),
                    SourceRefClassName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceRefLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceRefTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    LevelUpChoiceId = table.Column<string>(type: "TEXT", nullable: true),
                    FeatInstanceId = table.Column<string>(type: "TEXT", nullable: true),
                    Maximum = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_ability_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_ability_adjustments_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_casts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    CastId = table.Column<string>(type: "TEXT", nullable: false),
                    AttacksRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    InspirationWindowId = table.Column<string>(type: "TEXT", nullable: true),
                    Ritual = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentResourceId = table.Column<string>(type: "TEXT", nullable: false),
                    EffectTotal = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviewAttackRollKind = table.Column<string>(type: "TEXT", nullable: true),
                    PreviewAttackRollCount = table.Column<int>(type: "INTEGER", nullable: true),
                    HasCastingModifierCalculation = table.Column<bool>(type: "INTEGER", nullable: false),
                    CastingModifierTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    HasSaveDcCalculation = table.Column<bool>(type: "INTEGER", nullable: false),
                    SaveDcTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    SpellId = table.Column<string>(type: "TEXT", nullable: false),
                    SpellName = table.Column<string>(type: "TEXT", nullable: false),
                    SlotLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectKind = table.Column<string>(type: "TEXT", nullable: false),
                    DiceCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DiceSides = table.Column<int>(type: "INTEGER", nullable: true),
                    Modifier = table.Column<int>(type: "INTEGER", nullable: false),
                    FormulaText = table.Column<string>(type: "TEXT", nullable: false),
                    DamageType = table.Column<string>(type: "TEXT", nullable: true),
                    RadiusFt = table.Column<int>(type: "INTEGER", nullable: true),
                    SaveAbility = table.Column<string>(type: "TEXT", nullable: true),
                    SaveDc = table.Column<int>(type: "INTEGER", nullable: true),
                    Concentration = table.Column<bool>(type: "INTEGER", nullable: false),
                    DurationText = table.Column<string>(type: "TEXT", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    Modeled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_casts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_casts_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_choices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    Number = table.Column<int>(type: "INTEGER", nullable: true),
                    Flag = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_choices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_choices_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_class_tracks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<string>(type: "TEXT", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    RulesSource = table.Column<string>(type: "TEXT", nullable: true),
                    Subclass = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_class_tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_class_tracks_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_combat",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EncounterActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EncounterId = table.Column<string>(type: "TEXT", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnTurnNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    TurnId = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    PendingOwnTurnBoundaryId = table.Column<string>(type: "TEXT", nullable: false),
                    ActionAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    BonusActionAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReactionAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttacksRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementSpent = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementGranted = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotCastThisTurn = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasDeathSavesAtTurnStart = table.Column<bool>(type: "INTEGER", nullable: false),
                    TurnStartSuccesses = table.Column<int>(type: "INTEGER", nullable: false),
                    TurnStartFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    TurnStartStable = table.Column<bool>(type: "INTEGER", nullable: false),
                    TurnStartDead = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeathSaveRecordedTurnId = table.Column<string>(type: "TEXT", nullable: false),
                    LayoutVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    HasAttackSelection = table.Column<bool>(type: "INTEGER", nullable: false),
                    SelectionCastId = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionRollId = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionTarget = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionMarkEffectId = table.Column<string>(type: "TEXT", nullable: false),
                    HasSelectionProfile = table.Column<bool>(type: "INTEGER", nullable: false),
                    SelectionProfileName = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionDiceCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SelectionDiceSides = table.Column<int>(type: "INTEGER", nullable: true),
                    SelectionModifier = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectionFixedDamage = table.Column<int>(type: "INTEGER", nullable: true),
                    SelectionDamageType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_combat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_combat_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_combat_grants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantId = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Remaining = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_combat_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_combat_grants_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_combat_timers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalUnit = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    Remaining = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    AwaitingSave = table.Column<bool>(type: "INTEGER", nullable: false),
                    SavePrompt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_combat_timers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_combat_timers_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_companion_actions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    CompanionPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Rules = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_companion_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_companion_actions_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_companions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanionId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ArmorClass = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumHp = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHp = table.Column<int>(type: "INTEGER", nullable: false),
                    TemporaryHp = table.Column<int>(type: "INTEGER", nullable: false),
                    Speed = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_companions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_companions_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_defenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_defenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_defenses_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_effects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    RemainingRounds = table.Column<int>(type: "INTEGER", nullable: true),
                    Concentration = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_effects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_effects_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_feats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceId = table.Column<string>(type: "TEXT", nullable: false),
                    FeatId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Repeatable = table.Column<bool>(type: "INTEGER", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceClassName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    LevelTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    AbilityIncreaseInBase = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_feats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_feats_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_feature_configurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    FeatureId = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentValue = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_feature_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_feature_configurations_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_hit_die_pools",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    Sides = table.Column<int>(type: "INTEGER", nullable: false),
                    Maximum = table.Column<int>(type: "INTEGER", nullable: false),
                    Remaining = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_hit_die_pools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_hit_die_pools_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_hp_gains",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    BaseGain = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_hp_gains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_hp_gains_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_hp_rolls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_hp_rolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_hp_rolls_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Equipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresAttunement = table.Column<bool>(type: "INTEGER", nullable: false),
                    Attuned = table.Column<bool>(type: "INTEGER", nullable: false),
                    Carried = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnitWeightLb = table.Column<decimal>(type: "TEXT", nullable: true),
                    HasCharges = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChargeUseCost = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeMaximum = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeRecoveryNote = table.Column<string>(type: "TEXT", nullable: false),
                    UseAction = table.Column<string>(type: "TEXT", nullable: false),
                    Consumable = table.Column<bool>(type: "INTEGER", nullable: false),
                    BaseWeapon = table.Column<string>(type: "TEXT", nullable: false),
                    AttackBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageBonus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_items_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_journal_notes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    NoteId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_journal_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_journal_notes_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_level_up_choices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    LevelUpPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ChoiceId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_level_up_choices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_level_up_choices_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_level_ups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", nullable: false),
                    FromLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ToLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: false),
                    HpIncrease = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_level_ups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_level_ups_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_list_values",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    List = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    Number = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_list_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_list_values_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_movement_modes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Speed = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_movement_modes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_movement_modes_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_play_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptsCaptured = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCombat = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasSpellSources = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasHitDiePools = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasDeathSaves = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeathSaveSuccesses = table.Column<int>(type: "INTEGER", nullable: false),
                    DeathSaveFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    Stable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Dead = table.Column<bool>(type: "INTEGER", nullable: false),
                    HeroicInspiration = table.Column<bool>(type: "INTEGER", nullable: true),
                    CurrentHp = table.Column<int>(type: "INTEGER", nullable: false),
                    TemporaryHp = table.Column<int>(type: "INTEGER", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Exhaustion = table.Column<int>(type: "INTEGER", nullable: false),
                    DreadUsedThisTurn = table.Column<bool>(type: "INTEGER", nullable: false),
                    HunterTarget = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_play_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_play_snapshots_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_resources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Remaining = table.Column<int>(type: "INTEGER", nullable: false),
                    Maximum = table.Column<int>(type: "INTEGER", nullable: false),
                    Recovery = table.Column<string>(type: "TEXT", nullable: false),
                    SlotLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    ShortRestRecoveryAmount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_resources_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_roll_damage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DieSides = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DamageType = table.Column<string>(type: "TEXT", nullable: false),
                    Modifier = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_roll_damage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_roll_damage_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_rolls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    RollId = table.Column<string>(type: "TEXT", nullable: false),
                    AttackKind = table.Column<string>(type: "TEXT", nullable: false),
                    BonusDieSides = table.Column<int>(type: "INTEGER", nullable: true),
                    InspirationWindowId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    Advantage = table.Column<string>(type: "TEXT", nullable: false),
                    StandingBonusTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    FlatBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    ChosenD20 = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    LuckyUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Hit = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_rolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_rolls_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_spell_sources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false),
                    ClassTrackId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Ability = table.Column<string>(type: "TEXT", nullable: false),
                    MaximumSpellLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    CanChangeCantripsOnLongRest = table.Column<bool>(type: "INTEGER", nullable: false),
                    CantripReplacementLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    CanChangeOnLongRest = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreparedLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplacementLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    FromSpellbook = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_spell_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_spell_sources_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_undo_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    HasAfter = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_undo_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_character_undo_entries_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_ability_adjustments_CharacterId",
                table: "character_ability_adjustments",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_casts_CharacterId_Slot",
                table: "character_casts",
                columns: new[] { "CharacterId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_choices_CharacterId_Owner_Path",
                table: "character_choices",
                columns: new[] { "CharacterId", "Owner", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_class_tracks_CharacterId",
                table: "character_class_tracks",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_combat_CharacterId_Slot",
                table: "character_combat",
                columns: new[] { "CharacterId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_combat_grants_CharacterId",
                table: "character_combat_grants",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_combat_timers_CharacterId",
                table: "character_combat_timers",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_companion_actions_CharacterId",
                table: "character_companion_actions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_companions_CharacterId",
                table: "character_companions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_defenses_CharacterId",
                table: "character_defenses",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_effects_CharacterId",
                table: "character_effects",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_feats_CharacterId",
                table: "character_feats",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_feature_configurations_CharacterId",
                table: "character_feature_configurations",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_hit_die_pools_CharacterId",
                table: "character_hit_die_pools",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_hp_gains_CharacterId",
                table: "character_hp_gains",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_hp_rolls_CharacterId",
                table: "character_hp_rolls",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_items_CharacterId",
                table: "character_items",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_journal_notes_CharacterId",
                table: "character_journal_notes",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_level_up_choices_CharacterId",
                table: "character_level_up_choices",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_level_ups_CharacterId",
                table: "character_level_ups",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_list_values_CharacterId_Slot_Owner_List_Position",
                table: "character_list_values",
                columns: new[] { "CharacterId", "Slot", "Owner", "List", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_movement_modes_CharacterId",
                table: "character_movement_modes",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_play_snapshots_CharacterId_Slot",
                table: "character_play_snapshots",
                columns: new[] { "CharacterId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_resources_CharacterId",
                table: "character_resources",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_roll_damage_CharacterId",
                table: "character_roll_damage",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_rolls_CharacterId_Slot",
                table: "character_rolls",
                columns: new[] { "CharacterId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_spell_sources_CharacterId",
                table: "character_spell_sources",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_character_undo_entries_CharacterId",
                table: "character_undo_entries",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_characters_OwnerId",
                table: "characters",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_ability_adjustments");

            migrationBuilder.DropTable(
                name: "character_casts");

            migrationBuilder.DropTable(
                name: "character_choices");

            migrationBuilder.DropTable(
                name: "character_class_tracks");

            migrationBuilder.DropTable(
                name: "character_combat");

            migrationBuilder.DropTable(
                name: "character_combat_grants");

            migrationBuilder.DropTable(
                name: "character_combat_timers");

            migrationBuilder.DropTable(
                name: "character_companion_actions");

            migrationBuilder.DropTable(
                name: "character_companions");

            migrationBuilder.DropTable(
                name: "character_defenses");

            migrationBuilder.DropTable(
                name: "character_effects");

            migrationBuilder.DropTable(
                name: "character_feats");

            migrationBuilder.DropTable(
                name: "character_feature_configurations");

            migrationBuilder.DropTable(
                name: "character_hit_die_pools");

            migrationBuilder.DropTable(
                name: "character_hp_gains");

            migrationBuilder.DropTable(
                name: "character_hp_rolls");

            migrationBuilder.DropTable(
                name: "character_items");

            migrationBuilder.DropTable(
                name: "character_journal_notes");

            migrationBuilder.DropTable(
                name: "character_level_up_choices");

            migrationBuilder.DropTable(
                name: "character_level_ups");

            migrationBuilder.DropTable(
                name: "character_list_values");

            migrationBuilder.DropTable(
                name: "character_movement_modes");

            migrationBuilder.DropTable(
                name: "character_play_snapshots");

            migrationBuilder.DropTable(
                name: "character_resources");

            migrationBuilder.DropTable(
                name: "character_roll_damage");

            migrationBuilder.DropTable(
                name: "character_rolls");

            migrationBuilder.DropTable(
                name: "character_spell_sources");

            migrationBuilder.DropTable(
                name: "character_undo_entries");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
