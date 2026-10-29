using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropBookingAttachmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_attachments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false)
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
        }
    }
}
