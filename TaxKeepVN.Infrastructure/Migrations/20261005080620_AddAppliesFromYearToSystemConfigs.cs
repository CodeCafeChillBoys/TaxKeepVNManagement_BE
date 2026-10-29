using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppliesFromYearToSystemConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_system_configs_key",
                table: "system_configs");

            migrationBuilder.AddColumn<int>(
                name: "applies_from_year",
                table: "system_configs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_system_configs_key_year",
                table: "system_configs",
                columns: new[] { "config_key", "applies_from_year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_system_configs_key_year",
                table: "system_configs");

            migrationBuilder.DropColumn(
                name: "applies_from_year",
                table: "system_configs");

            migrationBuilder.CreateIndex(
                name: "uq_system_configs_key",
                table: "system_configs",
                column: "config_key",
                unique: true);
        }
    }
}
