using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Request tải lên và đính kèm chứng chỉ (Multipart/form-data)
    /// </summary>
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
