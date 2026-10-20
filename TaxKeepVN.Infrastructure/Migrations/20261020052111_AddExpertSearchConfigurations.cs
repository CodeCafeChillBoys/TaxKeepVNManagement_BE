using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertSearchConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expert_search_configurations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    default_sort_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    require_verified_certificate = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_full_name = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_job_title = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_company_name = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_bio = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_specializations = table.Column<bool>(type: "boolean", nullable: false),
                    search_in_certificates = table.Column<bool>(type: "boolean", nullable: false),
                    rating_weight = table.Column<double>(type: "double precision", nullable: false),
                    completed_consultations_weight = table.Column<double>(type: "double precision", nullable: false),
                    reviews_count_weight = table.Column<double>(type: "double precision", nullable: false),
                    slot_availability_weight = table.Column<double>(type: "double precision", nullable: false),
                    verified_certificate_weight = table.Column<double>(type: "double precision", nullable: false),
                    featured_specialization_ids = table.Column<string>(type: "text", nullable: true),
                    allowed_sort_options = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_search_configurations", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "expert_search_configurations",
                columns: new[] { "id", "allowed_sort_options", "completed_consultations_weight", "default_sort_by", "featured_specialization_ids", "rating_weight", "require_verified_certificate", "reviews_count_weight", "search_in_bio", "search_in_certificates", "search_in_company_name", "search_in_full_name", "search_in_job_title", "search_in_specializations", "slot_availability_weight", "updated_at", "verified_certificate_weight" },
                values: new object[] { 1, "[\"Recommended\",\"RatingDesc\",\"CompletedSessionsDesc\",\"FeeAsc\",\"FeeDesc\",\"ExperienceDesc\"]", 0.25, "Recommended", "[]", 0.34999999999999998, false, 0.14999999999999999, true, true, true, true, true, true, 0.14999999999999999, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0.10000000000000001 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expert_search_configurations");
        }
    }
}
