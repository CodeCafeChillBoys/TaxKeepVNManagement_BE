using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependentDocumentRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE dependents DROP COLUMN IF EXISTS date_of_birth;");
            migrationBuilder.Sql("ALTER TABLE income_sources ADD COLUMN IF NOT EXISTS tax_withheld numeric(18,2) NOT NULL DEFAULT 0.0;");
            migrationBuilder.Sql("ALTER TABLE income_sources ADD COLUMN IF NOT EXISTS tax_year integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE income_sources ADD COLUMN IF NOT EXISTS total_income numeric(18,2) NOT NULL DEFAULT 0.0;");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'dependents' AND column_name = 'IsProfileComplete') THEN
                        ALTER TABLE dependents ALTER COLUMN ""IsProfileComplete"" SET DEFAULT false;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'dependents' AND column_name = 'IsDeleted') THEN
                        ALTER TABLE dependents ALTER COLUMN ""IsDeleted"" SET DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS dependent_document_rules (
                    rule_id uuid NOT NULL,
                    target_group character varying(50) NOT NULL,
                    doc_type character varying(50) NOT NULL,
                    is_mandatory boolean NOT NULL DEFAULT true,
                    description text,
                    is_active boolean NOT NULL DEFAULT true,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_dependent_document_rules"" PRIMARY KEY (rule_id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS uq_group_doc_rule ON dependent_document_rules (target_group, doc_type);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dependent_document_rules");

            migrationBuilder.DropColumn(
                name: "tax_withheld",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_year",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "total_income",
                table: "income_sources");

            migrationBuilder.AlterColumn<bool>(
                name: "IsProfileComplete",
                table: "dependents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "dependents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "dependents",
                type: "date",
                nullable: true);
        }
    }
}
