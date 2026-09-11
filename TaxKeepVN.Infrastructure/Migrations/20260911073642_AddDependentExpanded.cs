using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependentExpanded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DependentDocuments_Dependents_DependentId",
                table: "DependentDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Dependents",
                table: "Dependents");

            migrationBuilder.RenameTable(
                name: "Dependents",
                newName: "dependents");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "dependents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TaxpayerId",
                table: "dependents",
                newName: "taxpayer_id");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "dependents",
                newName: "full_name");

            migrationBuilder.AddColumn<string>(
                name: "birth_cert_number",
                table: "dependents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citizen_id",
                table: "dependents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "dependents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "dependents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "effective_from_month",
                table: "dependents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "effective_to_month",
                table: "dependents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "dependents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relationship",
                table: "dependents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "dependents",
                type: "text",
                nullable: false,
                defaultValue: "PENDING_DOCUMENTS");

            migrationBuilder.AddColumn<string>(
                name: "tax_id_number",
                table: "dependents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "dependents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddPrimaryKey(
                name: "PK_dependents",
                table: "dependents",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_dependents_birth_cert",
                table: "dependents",
                column: "birth_cert_number");

            migrationBuilder.CreateIndex(
                name: "idx_dependents_citizen_id",
                table: "dependents",
                column: "citizen_id");

            migrationBuilder.CreateIndex(
                name: "idx_dependents_taxpayer_id",
                table: "dependents",
                column: "taxpayer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_DependentDocuments_dependents_DependentId",
                table: "DependentDocuments",
                column: "DependentId",
                principalTable: "dependents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DependentDocuments_dependents_DependentId",
                table: "DependentDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dependents",
                table: "dependents");

            migrationBuilder.DropIndex(
                name: "idx_dependents_birth_cert",
                table: "dependents");

            migrationBuilder.DropIndex(
                name: "idx_dependents_citizen_id",
                table: "dependents");

            migrationBuilder.DropIndex(
                name: "idx_dependents_taxpayer_id",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "birth_cert_number",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "citizen_id",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "effective_from_month",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "effective_to_month",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "note",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "relationship",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "status",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "tax_id_number",
                table: "dependents");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "dependents");

            migrationBuilder.RenameTable(
                name: "dependents",
                newName: "Dependents");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Dependents",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "taxpayer_id",
                table: "Dependents",
                newName: "TaxpayerId");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "Dependents",
                newName: "FullName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Dependents",
                table: "Dependents",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DependentDocuments_Dependents_DependentId",
                table: "DependentDocuments",
                column: "DependentId",
                principalTable: "Dependents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
