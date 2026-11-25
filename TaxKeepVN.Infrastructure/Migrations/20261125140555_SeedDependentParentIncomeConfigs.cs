using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDependentParentIncomeConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "system_configs",
                columns: new[] { "config_id", "applies_from_year", "config_key", "config_value", "created_at", "description", "is_active", "updated_at" },
                values: new object[,]
                {
                    { new Guid("44444444-0000-0000-0000-000000000026"), null, "DEPENDENT_PARENT_MALE_MAX_AGE", "62", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tuổi tối đa áp dụng cam kết thu nhập cho cha (tuổi)", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000027"), null, "DEPENDENT_PARENT_FEMALE_MAX_AGE", "60", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tuổi tối đa áp dụng cam kết thu nhập cho mẹ (tuổi)", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000028"), null, "DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND", "1000000", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ngưỡng thu nhập tháng tối đa cần cam kết của cha/mẹ (VNĐ)", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000028"));
        }
    }
}
