using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.config_id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_system_configs_key",
                table: "system_configs",
                column: "config_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_configs");
        }
    }
}
