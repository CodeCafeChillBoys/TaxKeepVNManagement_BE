using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Request tạo mới hoặc lưu cập nhật bản nháp hồ sơ chuyên gia (Draft)
    /// </summary>
    public class SaveExpertApplicationDraftRequest
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        [Required(ErrorMessage = "Chức danh / Nghề nghiệp không được để trống")]
        public string JobTitle { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string? Bio { get; set; }

        [Range(0, 70, ErrorMessage = "Số năm kinh nghiệm không hợp lệ (0 - 70)")]
        public int YearsOfExperience { get; set; } = 0;

        public string? CurrentPosition { get; set; }

        public string ExperienceDescription { get; set; } = string.Empty;

        /// <summary>Danh sách ID các lĩnh vực chuyên môn lựa chọn (bảng specializations)</summary>
        public List<int> SpecializationIds { get; set; } = new();

        /// <summary>Đề xuất mức phí tư vấn</summary>
        public List<FeeProposalRequestDto> FeeProposals { get; set; } = new();
    }
}
