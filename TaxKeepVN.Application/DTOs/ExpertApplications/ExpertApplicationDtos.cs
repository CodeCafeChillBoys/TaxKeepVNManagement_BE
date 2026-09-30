using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class ExpertApplicationDtos
    {
        // ── 1. REQUEST LƯU BẢN NHÁP (Draft) ──────────────────────────────────────
    public class SaveExpertApplicationDraftRequest
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        [Required(ErrorMessage = "Chức danh / Nghề nghiệp không được để trống")]
        public string JobTitle { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Bio { get; set; }
        [Range(0, 70, ErrorMessage = "Số năm kinh nghiệm không hợp lệ")]
        public int YearsOfExperience { get; set; } = 0;
        public string? CurrentPosition { get; set; }
        public string ExperienceDescription { get; set; } = string.Empty;
        /// <summary>Danh sách ID các lĩnh vực chuyên môn lựa chọn (bảng specializations)</summary>
        public List<int> SpecializationIds { get; set; } = new();
        /// <summary>Đề xuất mức phí tư vấn</summary>
        public List<FeeProposalRequestDto> FeeProposals { get; set; } = new();
    }

    public class FeeProposalRequestDto
    {
        [Required]
        public string SessionType { get; set; } = "ONLINE_MEETING"; // ONLINE_MEETING, CHAT, VOICE_CALL
        [Range(15, 240, ErrorMessage = "Thời lượng tư vấn từ 15 đến 240 phút")]
        public int DurationMinutes { get; set; } = 60;
        [Range(0, 100_000_000, ErrorMessage = "Mức phí đề xuất không hợp lệ")]
        public decimal ProposedFee { get; set; }
    }

     // ── 2. REQUEST TẢI LÊN CHỨNG CHỈ (Multipart Form Data) ──────────────────
    public class UploadCertificateRequest
    {
        [Required(ErrorMessage = "File chứng chỉ không được để trống")]
        public IFormFile File { get; set; } = null!;
        [Required(ErrorMessage = "Loại chứng chỉ không được để trống (CPA, TAX_AGENT, ...)")]
        public string CertificateType { get; set; } = string.Empty;
        [Required(ErrorMessage = "Tên chứng chỉ không được để trống")]
        public string CertificateName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Số hiệu chứng chỉ không được để trống")]
        public string CertificateNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "Đơn vị cấp chứng chỉ không được để trống")]
        public string IssuingAuthority { get; set; } = string.Empty;
        [Required(ErrorMessage = "Ngày cấp không được để trống")]
        public DateOnly IssueDate { get; set; }
        public bool HasExpiry { get; set; } = false;
        public DateOnly? ExpiryDate { get; set; }
    }


        
    }
}