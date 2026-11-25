using System.Collections.Generic;
using FluentAssertions;
using TaxKeepVN.Application.Helpers;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Helpers
{
    public class PeriodHelperTests
    {
        [Fact]
        public void AffectedYears_SamePeriod_ReturnsEmpty()
        {
            var result = PeriodHelper.AffectedYears("2025-01", "2026-12", "2025-01", "2026-12");
            result.Should().BeEmpty();
        }

        [Fact]
        public void AffectedYears_ShortenPeriodInsideSameYear_ReturnsOnlyThatYear()
        {
            // Cũ: 01/2025 - 12/2026, Mới: 01/2025 - 06/2026
            // Thay đổi tháng: 07/2026 -> 12/2026 (thuộc năm 2026). Năm 2025 không đổi!
            var result = PeriodHelper.AffectedYears("2025-01", "2026-12", "2025-01", "2026-06");
            result.Should().ContainSingle().Which.Should().Be(2026);
        }

        [Fact]
        public void AffectedYears_ShortenPeriodAffectingStartYear_ReturnsStartYear()
        {
            // Cũ: 01/2025 - 12/2026, Mới: 06/2025 - 12/2026
            // Thay đổi tháng: 01/2025 -> 05/2025 (thuộc năm 2025). Năm 2026 không đổi!
            var result = PeriodHelper.AffectedYears("2025-01", "2026-12", "2025-06", "2026-12");
            result.Should().ContainSingle().Which.Should().Be(2025);
        }

        [Fact]
        public void AffectedYears_ExtendPeriod_ReturnsOnlyNewYears()
        {
            // Cũ: 01/2025 - 12/2025, Mới: 01/2025 - 06/2026
            // Thay đổi tháng: 01/2026 -> 06/2026 (thuộc năm 2026)
            var result = PeriodHelper.AffectedYears("2025-01", "2025-12", "2025-01", "2026-06");
            result.Should().ContainSingle().Which.Should().Be(2026);
        }

        [Fact]
        public void AffectedYears_ShiftYears_ReturnsBothOldAndNewYears()
        {
            // Cũ: 01/2025 - 12/2025, Mới: 01/2026 - 12/2026
            var result = PeriodHelper.AffectedYears("2025-01", "2025-12", "2026-01", "2026-12");
            result.Should().BeEquivalentTo(new[] { 2025, 2026 });
        }

        [Fact]
        public void GetYearsInRange_ReturnsAllDistinctYears()
        {
            var result = PeriodHelper.GetYearsInRange("2024-05", "2026-02");
            result.Should().BeEquivalentTo(new[] { 2024, 2025, 2026 });
        }

        [Fact]
        public void GetMonthsInRange_InvalidFormatOrInvertedRange_ReturnsEmpty()
        {
            PeriodHelper.GetMonthsInRange("invalid", "2025-01").Should().BeEmpty();
            PeriodHelper.GetMonthsInRange("2026-01", "2025-01").Should().BeEmpty();
        }
    }
}
