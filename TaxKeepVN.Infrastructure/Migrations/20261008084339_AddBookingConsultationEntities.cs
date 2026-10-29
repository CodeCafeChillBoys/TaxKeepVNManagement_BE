using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingConsultationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hold_expires_at",
                table: "expert_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hold_user_id",
                table: "expert_slots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialization_id = table.Column<int>(type: "integer", nullable: false),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    topic_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    problem_description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING_PAYMENT"),
                    hold_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approval_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    refund_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "NONE"),
                    refunded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_bookings_expert_profiles_expert_profile_id",
                        column: x => x.expert_profile_id,
                        principalTable: "expert_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_expert_slots_expert_slot_id",
                        column: x => x.expert_slot_id,
                        principalTable: "expert_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_specializations_specialization_id",
                        column: x => x.specialization_id,
                        principalTable: "specializations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_attachments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_booking_attachments_booking_id",
                table: "booking_attachments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_expert_profile_id",
                table: "bookings",
                column: "expert_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_slot_id",
                table: "bookings",
                column: "expert_slot_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_status_hold",
                table: "bookings",
                columns: new[] { "status", "hold_expires_at" });

            migrationBuilder.CreateIndex(
                name: "idx_bookings_user_id",
                table: "bookings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_specialization_id",
                table: "bookings",
                column: "specialization_id");

            migrationBuilder.CreateIndex(
                name: "uq_bookings_booking_code",
                table: "bookings",
                column: "booking_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_attachments");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropColumn(
                name: "hold_expires_at",
                table: "expert_slots");

            migrationBuilder.DropColumn(
                name: "hold_user_id",
                table: "expert_slots");
        }
    }
}
