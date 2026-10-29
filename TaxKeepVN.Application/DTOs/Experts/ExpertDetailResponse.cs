using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Thông tin chi tiết hồ sơ chuyên gia hiển thị công khai (Đặc tả 2 - Mục 5 Luồng chính)
    /// Đã loại bỏ các thông tin nhạy cảm: CCCD, SĐT, Email cá nhân (BR-03)
    /// </summary>
    public class ExpertDetailResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Bio { get; set; }
        public int YearsOfExperience { get; set; }
        public decimal Rating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedConsultationsCount { get; set; }

        /// <summary>Chuyên gia có đang mở nhận lịch hay không</summary>
        public bool IsActive { get; set; }

        /// <summary>Danh sách các lĩnh vực chuyên môn</summary>
        public List<string> SpecializationNames { get; set; } = new();

        /// <summary>Danh sách các chứng chỉ chuyên môn đã được xác thực (che mờ số định danh cá nhân)</summary>
        public List<ExpertPublicCertificateDto> VerifiedCertificates { get; set; } = new();

        /// <summary>Bảng biểu phí tư vấn theo từng loại phiên & thời lượng</summary>
        public List<ExpertPublicFeeDto> FeePackages { get; set; } = new();

        /// <summary>Danh sách các đánh giá, feedback thực tế gần đây</summary>
        public List<ExpertPublicReviewDto> RecentReviews { get; set; } = new();

        /// <summary>Danh sách các khung giờ rảnh sắp tới</summary>
        public List<ExpertAvailableSlotDto> UpcomingAvailableSlots { get; set; } = new();
    }
}
