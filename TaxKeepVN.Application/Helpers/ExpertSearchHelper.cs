using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Helpers
{
    /// <summary>
    /// Các phương thức trợ giúp xử lý nghiệp vụ cho tính năng Tìm kiếm và Xếp hạng chuyên gia (Đặc tả 2)
    /// </summary>
    public static class ExpertSearchHelper
    {
        /// <summary>
        /// Thuật toán tính điểm xếp hạng chuyên gia theo quy tắc nghiệp vụ BR-02
        /// Trọng số: Rating (50%), Hoàn thành ca tư vấn (25%), Tổng lượt đánh giá (10%), Lịch trống sớm (15%)
        /// </summary>
        public static double CalculateRankingScore(ExpertProfile profile, bool hasSlotSoon, bool hasAnySlot)
        {
            double ratingScore = (double)profile.Rating * 20.0; // Tối đa 100 điểm
            double completionScore = Math.Min(profile.CompletedConsultationsCount, 50) * 0.5; // Tối đa 25 điểm
            double reviewsScore = Math.Min(profile.TotalReviews, 50) * 0.3; // Tối đa 15 điểm
            double availabilityScore = hasSlotSoon ? 15.0 : (hasAnySlot ? 5.0 : -20.0); // Phạt nếu không còn slot nào trống

            var total = ratingScore + completionScore + reviewsScore + availabilityScore;
            return Math.Round(total, 2);
        }

        /// <summary>
        /// Che giấu số hiệu chứng chỉ bảo mật thông tin theo BR-03 (giữ lại 3 ký tự cuối, phía trước là dấu *)
        /// </summary>
        public static string MaskCertificateNumber(string certNumber)
        {
            if (string.IsNullOrWhiteSpace(certNumber)) return "***";
            if (certNumber.Length <= 4) return "****";

            // Giữ lại 3 ký tự cuối, các ký tự trước chuyển thành *
            var keepLength = 3;
            var maskedPart = new string('*', certNumber.Length - keepLength);
            var visiblePart = certNumber[^keepLength..];
            return $"{maskedPart}{visiblePart}";
        }

        /// <summary>
        /// Che giấu tên người đánh giá theo BR-03 (VD: "Nguyễn Văn Hùng" -> "Nguyễn V. H***")
        /// </summary>
        public static string MaskReviewerName(string fullName, bool isAnonymous)
        {
            if (isAnonymous || string.IsNullOrWhiteSpace(fullName))
            {
                return "Khách hàng ẩn danh";
            }

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                var single = parts[0];
                return single.Length > 2 ? $"{single[0]}***{single[^1]}" : $"{single[0]}*";
            }

            // Ví dụ: "Nguyễn Văn Hùng" -> "Nguyễn V. H***"
            var firstName = parts[0];
            var lastName = parts[^1];
            var maskedLastName = lastName.Length > 2 ? $"{lastName[0]}***" : $"{lastName[0]}*";
            return $"{firstName} {maskedLastName}";
        }

        /// <summary>
        /// Xử lý tạo gợi ý khi tìm kiếm không có kết quả phù hợp (Đặc tả 2 - Mục 6)
        /// </summary>
        public static ExpertSearchSuggestionDto BuildEmptySuggestions(ExpertSearchQueryParameters query)
        {
            return new ExpertSearchSuggestionDto
            {
                Message = "Không tìm thấy chuyên gia phù hợp với tiêu chí lọc hiện tại của bạn.",
                SuggestRelaxRating = query.MinRating.HasValue && query.MinRating.Value >= 4.0m,
                SuggestExpandPriceRange = query.MinFee.HasValue || query.MaxFee.HasValue,
                SuggestExpandTimeFilter = query.TimeFilter != ExpertTimeFilter.All,
                AiAssistantCtaUrl = "/assistant/chat",
                RequestSupportCtaUrl = "/consultation/request-match"
            };
        }
    }
}
