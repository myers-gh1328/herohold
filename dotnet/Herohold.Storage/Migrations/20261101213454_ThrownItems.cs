using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herohold.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ThrownItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThrownDamageCount",
                table: "items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThrownDamageSides",
                table: "items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThrownDamageType",
                table: "items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ThrownRangeFt",
                table: "items",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThrownDamageCount",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ThrownDamageSides",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ThrownDamageType",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ThrownRangeFt",
                table: "items");
        }
    }
}
