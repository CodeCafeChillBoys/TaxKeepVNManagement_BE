using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dependents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxpayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DependentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DependentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileMimeType = table.Column<string>(type: "text", nullable: false),
                    IsReadable = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DependentDocuments_Dependents_DependentId",
                        column: x => x.DependentId,
                        principalTable: "Dependents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DependentDocuments_DependentId",
                table: "DependentDocuments",
                column: "DependentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DependentDocuments");

            migrationBuilder.DropTable(
                name: "Dependents");
        }
    }
}
