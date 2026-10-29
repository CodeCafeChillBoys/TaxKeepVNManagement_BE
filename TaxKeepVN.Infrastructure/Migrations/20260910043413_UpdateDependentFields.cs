using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDependentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Dependents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CurrentGroup",
                table: "Dependents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Dependents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Dependents");

            migrationBuilder.DropColumn(
                name: "CurrentGroup",
                table: "Dependents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Dependents");
        }
    }
}
