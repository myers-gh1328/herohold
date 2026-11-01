using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herohold.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AmmunitionCounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountAmmunition",
                table: "characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountAmmunition",
                table: "characters");
        }
    }
}
