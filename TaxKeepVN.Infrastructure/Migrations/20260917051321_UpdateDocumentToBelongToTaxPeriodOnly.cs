using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDocumentToBelongToTaxPeriodOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_tax_periods_period_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_users_user_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "idx_documents_user_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "documents");

            migrationBuilder.AlterColumn<Guid>(
                name: "period_id",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_tax_periods_period_id",
                table: "documents",
                column: "period_id",
                principalTable: "tax_periods",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_tax_periods_period_id",
                table: "documents");

            migrationBuilder.AlterColumn<Guid>(
                name: "period_id",
                table: "documents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "idx_documents_user_id",
                table: "documents",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_tax_periods_period_id",
                table: "documents",
                column: "period_id",
                principalTable: "tax_periods",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_users_user_id",
                table: "documents",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
