using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    public class MedicalStatement
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Khóa ngoại liên kết với Hóa đơn viện phí (Document.Id)</summary>
        public Guid DocumentId { get; set; }

        /// <summary>Đường dẫn file ảnh / PDF của Bảng kê chi phí KCB</summary>
        public string FileUrl { get; set; } = string.Empty;

        /// <summary>Tên file gốc khi tải lên</summary>
        public string? OriginalFilename { get; set; }

        /// <summary>Mẫu số bảng kê (VD: 01/BV, 01/KBCB)</summary>
        public string? FormCode { get; set; }

        /// <summary>Số bảng kê</summary>
        public string? StatementNumber { get; set; }

        /// <summary>Họ tên người bệnh</summary>
        public string? PatientName { get; set; }

        /// <summary>Ngày tháng năm sinh người bệnh</summary>
        public DateOnly? PatientDob { get; set; }

        /// <summary>Giới tính người bệnh</summary>
        public string? PatientGender { get; set; }

        /// <summary>Số CCCD/CMND người bệnh</summary>
        public string? PatientIdCard { get; set; }

        /// <summary>Mã số thẻ BHYT</summary>
        public string? InsuranceNumber { get; set; }

        /// <summary>Mức hưởng BHYT (VD: 80, 100)</summary>
        public decimal? BenefitRate { get; set; }

        /// <summary>Tên cơ sở khám chữa bệnh</summary>
        public string? HospitalName { get; set; }

        /// <summary>Mã cơ sở khám chữa bệnh</summary>
        public string? HospitalCode { get; set; }

        /// <summary>Khoa phòng điều trị</summary>
        public string? Department { get; set; }

        /// <summary>Ngày vào viện</summary>
        public DateOnly? AdmissionDate { get; set; }

        /// <summary>Ngày ra viện</summary>
        public DateOnly? DischargeDate { get; set; }

        /// <summary>Hình thức điều trị (Ngoại trú / Nội trú)</summary>
        public string? TreatmentType { get; set; }

        /// <summary>Tổng chi phí khám chữa bệnh (Cột 7 / Tổng chi phí)</summary>
        public decimal? TotalHospitalAmount { get; set; }

        /// <summary>Số tiền do quỹ BHYT thanh toán (Cột 10)</summary>
        public decimal? InsurancePaidAmount { get; set; }

        /// <summary>Số tiền người bệnh cùng chi trả (Cột 11)</summary>
        public decimal? PatientPaidAmount { get; set; }

        /// <summary>Số tiền người bệnh tự trả ngoài danh mục BHYT (Cột 13)</summary>
        public decimal? PatientSelfPayAmount { get; set; }

        /// <summary>Tổng số tiền hợp lệ để giảm trừ thuế TNCN</summary>
        public decimal? TotalEligibleAmount { get; set; }

        /// <summary>Điểm tin cậy OCR tổng thể (0.0 đến 1.0)</summary>
        public decimal? OverallConfidence { get; set; }

        /// <summary>Ngưỡng tin cậy áp dụng</summary>
        public decimal? AppliedThreshold { get; set; }

        /// <summary>Vượt qua ngưỡng tin cậy</summary>
        public bool? IsPassedThreshold { get; set; }

        /// <summary>UPLOADED, EXTRACTED, CONFIRMED, FAILED</summary>
        public string Status { get; set; } = "UPLOADED";

        /// <summary>Các cảnh báo hoặc lỗi đối soát / xác thực</summary>
        public string? ValidationErrors { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public Document? Document { get; set; }
        public ICollection<MedicalStatementLine> Lines { get; set; } = new List<MedicalStatementLine>();
    }
}
