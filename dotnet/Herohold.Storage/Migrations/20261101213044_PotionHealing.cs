using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herohold.Storage.Migrations
{
    /// <inheritdoc />
    public partial class PotionHealing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HealingBonus",
                table: "items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HealingDiceCount",
                table: "items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealingDiceSides",
                table: "items",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealingBonus",
                table: "items");

            migrationBuilder.DropColumn(
                name: "HealingDiceCount",
                table: "items");

            migrationBuilder.DropColumn(
                name: "HealingDiceSides",
                table: "items");
        }
    }
}
