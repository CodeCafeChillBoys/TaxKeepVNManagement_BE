using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price_monthly = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    price_yearly = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    max_ai_questions_per_month = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    chat_history_retention_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    max_ocr_scans_per_month = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    max_income_sources = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    max_dependents = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    can_export_pdf = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    can_export_zip = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    booking_discount_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    free_consultations_per_year = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_quota_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ai_questions_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ocr_scans_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_quota_usages", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_quota_usages_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "free"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    amount_paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "FREE"),
                    payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "free"),
                    payment_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    snapshot_max_ai_questions_per_month = table.Column<int>(type: "integer", nullable: false),
                    snapshot_chat_history_retention_days = table.Column<int>(type: "integer", nullable: false),
                    snapshot_max_ocr_scans_per_month = table.Column<int>(type: "integer", nullable: false),
                    snapshot_max_income_sources = table.Column<int>(type: "integer", nullable: false),
                    snapshot_max_dependents = table.Column<int>(type: "integer", nullable: false),
                    snapshot_can_export_pdf = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_can_export_zip = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_booking_discount_percent = table.Column<int>(type: "integer", nullable: false),
                    snapshot_free_consultations_per_year = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_subscription_plans_slug",
                table: "subscription_plans",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_subscription_quota_usages_user_period",
                table: "subscription_quota_usages",
                columns: new[] { "user_id", "period_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_expires_at",
                table: "user_subscriptions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_user_subscriptions_user_status",
                table: "user_subscriptions",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_plan_id",
                table: "user_subscriptions",
                column: "plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_quota_usages");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
