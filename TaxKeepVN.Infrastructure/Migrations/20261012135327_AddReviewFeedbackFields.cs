using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewFeedbackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detail_scores_json",
                table: "expert_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expert_replied_at",
                table: "expert_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "expert_reply",
                table: "expert_reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_flagged_for_review",
                table: "expert_reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "review_deadline",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_expert_reviews_booking_id",
                table: "expert_reviews",
                column: "booking_id",
                unique: true,
                filter: "booking_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_expert_reviews_booking_id",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "detail_scores_json",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "expert_replied_at",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "expert_reply",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "is_flagged_for_review",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "review_deadline",
                table: "bookings");
        }
    }
}
