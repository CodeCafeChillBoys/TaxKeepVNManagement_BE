using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIncomeAndDependentLimitsFromSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "snapshot_max_dependents",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "snapshot_max_income_sources",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "max_dependents",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "max_income_sources",
                table: "subscription_plans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "snapshot_max_dependents",
                table: "user_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "snapshot_max_income_sources",
                table: "user_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_dependents",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "max_income_sources",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
