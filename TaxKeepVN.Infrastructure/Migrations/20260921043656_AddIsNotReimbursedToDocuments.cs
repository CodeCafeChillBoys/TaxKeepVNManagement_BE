using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsNotReimbursedToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents");

            migrationBuilder.DropIndex(
                name: "IX_income_sources_taxpayer_id",
                table: "income_sources");

            migrationBuilder.RenameIndex(
                name: "IX_dependent_documents_dependent_id",
                table: "dependent_documents",
                newName: "idx_dependent_documents_dependent_id");

            migrationBuilder.AddColumn<bool>(
                name: "is_not_reimbursed",
                table: "documents",
                type: "boolean",
                nullable: true,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_not_reimbursed",
                table: "documents");

            migrationBuilder.RenameIndex(
                name: "idx_dependent_documents_dependent_id",
                table: "dependent_documents",
                newName: "IX_dependent_documents_dependent_id");

            migrationBuilder.CreateIndex(
                name: "IX_income_sources_taxpayer_id",
                table: "income_sources",
                column: "taxpayer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents",
                column: "taxpayer_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
