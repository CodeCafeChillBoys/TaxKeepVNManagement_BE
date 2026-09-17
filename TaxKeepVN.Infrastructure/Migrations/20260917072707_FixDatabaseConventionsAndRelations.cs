using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDatabaseConventionsAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DependentDocuments_dependents_DependentId",
                table: "DependentDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DependentDocuments",
                table: "DependentDocuments");

            migrationBuilder.RenameTable(
                name: "DependentDocuments",
                newName: "dependent_documents");

            migrationBuilder.RenameColumn(
                name: "IsProfileComplete",
                table: "dependents",
                newName: "is_profile_complete");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "dependents",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "CurrentGroup",
                table: "dependents",
                newName: "current_group");

            migrationBuilder.RenameColumn(
                name: "BirthDate",
                table: "dependents",
                newName: "birth_date");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "dependent_documents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "dependent_documents",
                newName: "uploaded_at");

            migrationBuilder.RenameColumn(
                name: "IsReadable",
                table: "dependent_documents",
                newName: "is_readable");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "dependent_documents",
                newName: "file_url");

            migrationBuilder.RenameColumn(
                name: "FileMimeType",
                table: "dependent_documents",
                newName: "file_mime_type");

            migrationBuilder.RenameColumn(
                name: "DocType",
                table: "dependent_documents",
                newName: "doc_type");

            migrationBuilder.RenameColumn(
                name: "DependentId",
                table: "dependent_documents",
                newName: "dependent_id");

            migrationBuilder.RenameIndex(
                name: "IX_DependentDocuments_DependentId",
                table: "dependent_documents",
                newName: "IX_dependent_documents_dependent_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dependent_documents",
                table: "dependent_documents",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_system_notifications_user_id",
                table: "system_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_income_sources_taxpayer_id",
                table: "income_sources",
                column: "taxpayer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_dependent_documents_dependents_dependent_id",
                table: "dependent_documents",
                column: "dependent_id",
                principalTable: "dependents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents",
                column: "taxpayer_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_income_sources_users_taxpayer_id",
                table: "income_sources",
                column: "taxpayer_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_notifications_users_user_id",
                table: "system_notifications",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dependent_documents_dependents_dependent_id",
                table: "dependent_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_dependents_users_taxpayer_id",
                table: "dependents");

            migrationBuilder.DropForeignKey(
                name: "FK_income_sources_users_taxpayer_id",
                table: "income_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_system_notifications_users_user_id",
                table: "system_notifications");

            migrationBuilder.DropIndex(
                name: "IX_system_notifications_user_id",
                table: "system_notifications");

            migrationBuilder.DropIndex(
                name: "IX_income_sources_taxpayer_id",
                table: "income_sources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dependent_documents",
                table: "dependent_documents");

            migrationBuilder.RenameTable(
                name: "dependent_documents",
                newName: "DependentDocuments");

            migrationBuilder.RenameColumn(
                name: "is_profile_complete",
                table: "dependents",
                newName: "IsProfileComplete");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "dependents",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "current_group",
                table: "dependents",
                newName: "CurrentGroup");

            migrationBuilder.RenameColumn(
                name: "birth_date",
                table: "dependents",
                newName: "BirthDate");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "DependentDocuments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "uploaded_at",
                table: "DependentDocuments",
                newName: "UploadedAt");

            migrationBuilder.RenameColumn(
                name: "is_readable",
                table: "DependentDocuments",
                newName: "IsReadable");

            migrationBuilder.RenameColumn(
                name: "file_url",
                table: "DependentDocuments",
                newName: "FileUrl");

            migrationBuilder.RenameColumn(
                name: "file_mime_type",
                table: "DependentDocuments",
                newName: "FileMimeType");

            migrationBuilder.RenameColumn(
                name: "doc_type",
                table: "DependentDocuments",
                newName: "DocType");

            migrationBuilder.RenameColumn(
                name: "dependent_id",
                table: "DependentDocuments",
                newName: "DependentId");

            migrationBuilder.RenameIndex(
                name: "IX_dependent_documents_dependent_id",
                table: "DependentDocuments",
                newName: "IX_DependentDocuments_DependentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DependentDocuments",
                table: "DependentDocuments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DependentDocuments_dependents_DependentId",
                table: "DependentDocuments",
                column: "DependentId",
                principalTable: "dependents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
