using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExpertProfileIdFromBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_expert_profiles_expert_profile_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "idx_bookings_expert_profile_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "expert_profile_id",
                table: "bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "expert_profile_id",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "idx_bookings_expert_profile_id",
                table: "bookings",
                column: "expert_profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_expert_profiles_expert_profile_id",
                table: "bookings",
                column: "expert_profile_id",
                principalTable: "expert_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
