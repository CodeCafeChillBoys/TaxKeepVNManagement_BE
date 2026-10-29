using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Thông tin chuyên gia hiển thị trong danh sách tìm kiếm (dạng Card - Đặc tả 2)
    /// </summary>
    public class ExpertListItemResponse
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

        /// <summary>Mức phí khởi điểm từ (gói thấp nhất của chuyên gia)</summary>
        public decimal StartingFee { get; set; }

        /// <summary>Danh sách tên các chuyên môn/lĩnh vực tư vấn</summary>
        public List<string> SpecializationNames { get; set; } = new();

        /// <summary>Số lượng chứng chỉ chuyên môn đã được xác thực (Verified)</summary>
        public int VerifiedCertificateCount { get; set; }

        /// <summary>Có lịch trống rảnh trong tương lai gần không</summary>
        public bool HasAvailableSlotSoon { get; set; }

        /// <summary>Ngày rảnh sớm nhất sắp tới</summary>
        public DateOnly? EarliestAvailableDate { get; set; }

        /// <summary>Điểm thuật toán đề xuất (Ranking Score - BR-02)</summary>
        public double RankingScore { get; set; }
    }
}
