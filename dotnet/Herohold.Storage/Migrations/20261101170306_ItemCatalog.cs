using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herohold.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ItemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "RulesSource", table: "characters", type: "TEXT", nullable: false, defaultValue: "XPHB");

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresAttunement = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnitWeightLb = table.Column<decimal>(type: "TEXT", nullable: true),
                    HasCharges = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChargeUseCost = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeMaximum = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargeRecoveryNote = table.Column<string>(type: "TEXT", nullable: false),
                    UseAction = table.Column<string>(type: "TEXT", nullable: false),
                    Consumable = table.Column<bool>(type: "INTEGER", nullable: false),
                    BaseWeapon = table.Column<string>(type: "TEXT", nullable: false),
                    AttackBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageBonus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_items_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_defenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_defenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_defenses_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_effects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_item_effects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_effects_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_modifier_bonuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Ability = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_modifier_bonuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_modifier_bonuses_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<string>(name: "CatalogItemId", table: "character_items", type: "TEXT", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ScrollSpell", table: "character_items", type: "TEXT", nullable: false, defaultValue: "");

            // Each stored item becomes a "Legacy" catalog item with its definition; on start the store links each one to the
            // official item it names or keeps it as homebrew.
            migrationBuilder.Sql("""
                INSERT INTO items (Id, Source, Kind, OwnerId, Name, Description, RequiresAttunement, UnitWeightLb, HasCharges, ChargeUseCost, ChargeMaximum,
                    ChargeRecoveryNote, UseAction, Consumable, BaseWeapon, AttackBonus, DamageBonus)
                SELECT 'legacy:' || i.CharacterId || ':' || i.Position, 'Legacy', 'gear', c.OwnerId, i.Name, i.Description, i.RequiresAttunement, i.UnitWeightLb,
                    i.HasCharges, i.ChargeUseCost, i.ChargeMaximum, i.ChargeRecoveryNote, i.UseAction, i.Consumable, i.BaseWeapon, i.AttackBonus, i.DamageBonus
                FROM character_items i JOIN characters c ON c.Id = i.CharacterId;
                UPDATE character_items SET CatalogItemId = 'legacy:' || CharacterId || ':' || Position;
                INSERT INTO item_modifier_bonuses (ItemId, Position, Ability, Value)
                SELECT 'legacy:' || CharacterId || ':' || substr(Owner, 6), Position, Key, coalesce(Number, 0)
                FROM character_list_values WHERE Slot = 0 AND Owner LIKE 'item:%' AND List = 'modifier_bonuses' AND Position >= 0;
                INSERT INTO item_defenses (ItemId, Position, Kind, Type, Source, Note)
                SELECT 'legacy:' || CharacterId || ':' || substr(Owner, 6), Position, Kind, Type, Source, Note
                FROM character_defenses WHERE Owner LIKE 'item:%';
                INSERT INTO item_effects (ItemId, Owner, Position, EffectId, Name, Source, Note, RemainingRounds, Concentration)
                SELECT 'legacy:' || CharacterId || ':' || substr(Owner, 6, instr(substr(Owner, 6), ':') - 1), substr(Owner, 6 + instr(substr(Owner, 6), ':')),
                    Position, EffectId, Name, Source, Note, RemainingRounds, Concentration
                FROM character_effects WHERE Slot = 0 AND Owner LIKE 'item:%:%';
                DELETE FROM character_list_values WHERE Owner LIKE 'item:%';
                DELETE FROM character_defenses WHERE Owner LIKE 'item:%';
                DELETE FROM character_effects WHERE Owner LIKE 'item:%';
                """);

            migrationBuilder.DropColumn(name: "Name", table: "character_items");
            migrationBuilder.DropColumn(name: "Description", table: "character_items");
            migrationBuilder.DropColumn(name: "RequiresAttunement", table: "character_items");
            migrationBuilder.DropColumn(name: "UnitWeightLb", table: "character_items");
            migrationBuilder.DropColumn(name: "HasCharges", table: "character_items");
            migrationBuilder.DropColumn(name: "ChargeUseCost", table: "character_items");
            migrationBuilder.DropColumn(name: "ChargeMaximum", table: "character_items");
            migrationBuilder.DropColumn(name: "ChargeRecoveryNote", table: "character_items");
            migrationBuilder.DropColumn(name: "UseAction", table: "character_items");
            migrationBuilder.DropColumn(name: "Consumable", table: "character_items");
            migrationBuilder.DropColumn(name: "BaseWeapon", table: "character_items");
            migrationBuilder.DropColumn(name: "AttackBonus", table: "character_items");
            migrationBuilder.DropColumn(name: "DamageBonus", table: "character_items");

            migrationBuilder.CreateIndex(
                name: "IX_character_items_CatalogItemId",
                table: "character_items",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_defenses_ItemId",
                table: "item_defenses",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_effects_ItemId",
                table: "item_effects",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_modifier_bonuses_ItemId",
                table: "item_modifier_bonuses",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_items_OwnerId",
                table: "items",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_character_items_items_CatalogItemId",
                table: "character_items",
                column: "CatalogItemId",
                principalTable: "items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Item definitions moved into the shared catalog; going back means restoring the backup taken before the upgrade.
            throw new NotSupportedException("Restore the database backup taken before the item catalog upgrade.");
        }
    }
}
