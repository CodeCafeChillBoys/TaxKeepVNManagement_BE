using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertBankAccountAndWithdrawalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expert_bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_bank_accounts_expert_profiles_expert_profile_id",
                        column: x => x.expert_profile_id,
                        principalTable: "expert_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expert_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    bank_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    transfer_proof_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bank_transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    transferred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_expert_profiles_expert_profile_id",
                        column: x => x.expert_profile_id,
                        principalTable: "expert_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_expert_bank_accounts_profile_id",
                table: "expert_bank_accounts",
                column: "expert_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_withdrawal_requests_profile_id",
                table: "withdrawal_requests",
                column: "expert_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_withdrawal_requests_status",
                table: "withdrawal_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_reviewed_by",
                table: "withdrawal_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "uq_withdrawal_requests_code",
                table: "withdrawal_requests",
                column: "request_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expert_bank_accounts");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");
        }
    }
}
