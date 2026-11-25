using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependentCatalogTablesAndAlternativeGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "alternative_group",
                table: "dependent_document_rules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "dependent_document_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ADMIN");

            migrationBuilder.CreateTable(
                name: "dependent_relationships",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependent_relationships", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "dependent_groups",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    relationship_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    min_age = table.Column<int>(type: "integer", nullable: true),
                    max_age = table.Column<int>(type: "integer", nullable: true),
                    require_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    require_studying = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ADMIN"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependent_groups", x => x.code);
                    table.ForeignKey(
                        name: "FK_dependent_groups_dependent_relationships_relationship_code",
                        column: x => x.relationship_code,
                        principalTable: "dependent_relationships",
                        principalColumn: "code",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "dependent_relationships",
                columns: new[] { "code", "display_order", "is_active", "name" },
                values: new object[,]
                {
                    { "CHILD", 1, true, "Con" },
                    { "OTHER_DEPENDENT", 4, true, "Cá nhân khác không nơi nương tựa" },
                    { "PARENT", 3, true, "Cha/mẹ" },
                    { "SPOUSE", 2, true, "Vợ/chồng" }
                });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "level", "max_age", "min_age", "name", "relationship_code", "require_disabled", "source", "updated_at" },
                values: new object[] { "CHILD_OVER_18_DISABLED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, 1, null, 18, "Con từ 18 tuổi trở lên bị khuyết tật", "CHILD", true, "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "level", "max_age", "min_age", "name", "relationship_code", "require_studying", "source", "updated_at" },
                values: new object[] { "CHILD_OVER_18_STUDYING", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, 1, null, 18, "Con từ 18 tuổi trở lên đang đi học", "CHILD", true, "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "max_age", "min_age", "name", "relationship_code", "source", "updated_at" },
                values: new object[,]
                {
                    { "CHILD_UNDER_18", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, 18, null, "Con dưới 18 tuổi", "CHILD", "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "OTHER_HELPLESS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 8, true, null, null, "Cá nhân không nơi nương tựa khác", "OTHER_DEPENDENT", "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "max_age", "min_age", "name", "relationship_code", "require_disabled", "source", "updated_at" },
                values: new object[] { "PARENT_DISABLED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, null, null, "Cha/Mẹ khuyết tật", "PARENT", true, "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "level", "max_age", "min_age", "name", "relationship_code", "source", "updated_at" },
                values: new object[] { "PARENT_RETIRED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, 1, null, null, "Cha/Mẹ ngoài độ tuổi lao động", "PARENT", "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "max_age", "min_age", "name", "relationship_code", "require_disabled", "source", "updated_at" },
                values: new object[] { "SPOUSE_DISABLED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, null, null, "Vợ/Chồng khuyết tật", "SPOUSE", true, "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "dependent_groups",
                columns: new[] { "code", "created_at", "display_order", "is_active", "level", "max_age", "min_age", "name", "relationship_code", "source", "updated_at" },
                values: new object[] { "SPOUSE_RETIRED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, 1, null, null, "Vợ/Chồng ngoài độ tuổi lao động", "SPOUSE", "SEED", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_dependent_groups_relationship_code",
                table: "dependent_groups",
                column: "relationship_code");

            migrationBuilder.Sql("UPDATE dependent_document_rules SET alternative_group = 'IDENTITY_PROOF' WHERE target_group = 'CHILD_UNDER_18' AND doc_type IN ('BIRTH_CERTIFICATE', 'CITIZEN_ID');");
            migrationBuilder.Sql("UPDATE dependent_document_rules SET alternative_group = 'RELATION_PROOF' WHERE target_group IN ('PARENT_DISABLED', 'PARENT_RETIRED') AND doc_type IN ('RELATIONSHIP_CERTIFICATE', 'BIRTH_CERTIFICATE');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dependent_groups");

            migrationBuilder.DropTable(
                name: "dependent_relationships");

            migrationBuilder.DropColumn(
                name: "alternative_group",
                table: "dependent_document_rules");

            migrationBuilder.DropColumn(
                name: "source",
                table: "dependent_document_rules");
        }
    }
}
