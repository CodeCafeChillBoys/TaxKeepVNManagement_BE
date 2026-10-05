using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertSlotsAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "completed_consultations_count",
                table: "expert_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "expert_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_reviews_expert_profiles_expert_profile_id",
                        column: x => x.expert_profile_id,
                        principalTable: "expert_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expert_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expert_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ONLINE_MEETING"),
                    is_booked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_slots_expert_profiles_expert_profile_id",
                        column: x => x.expert_profile_id,
                        principalTable: "expert_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_expert_reviews_profile_id",
                table: "expert_reviews",
                column: "expert_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_reviews_user_id",
                table: "expert_reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_slots_profile_id",
                table: "expert_slots",
                column: "expert_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_slots_slot_date",
                table: "expert_slots",
                column: "slot_date");

            migrationBuilder.CreateIndex(
                name: "uq_expert_slots_profile_datetime",
                table: "expert_slots",
                columns: new[] { "expert_profile_id", "slot_date", "start_time" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expert_reviews");

            migrationBuilder.DropTable(
                name: "expert_slots");

            migrationBuilder.DropColumn(
                name: "completed_consultations_count",
                table: "expert_profiles");
        }
    }
}
