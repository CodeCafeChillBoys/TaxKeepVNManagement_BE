using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRelationsToDependentsAndTaxSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents",
                column: "taxpayer_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tax_settlement_dossiers_users_taxpayer_id",
                table: "tax_settlement_dossiers",
                column: "taxpayer_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents");

            migrationBuilder.DropForeignKey(
                name: "FK_tax_settlement_dossiers_users_taxpayer_id",
                table: "tax_settlement_dossiers");
        }
    }
}
