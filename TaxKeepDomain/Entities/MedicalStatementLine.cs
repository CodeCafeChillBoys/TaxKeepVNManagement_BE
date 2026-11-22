using System;

namespace TaxKeepVN.Domain.Entities
{
    public class MedicalStatementLine
    {
        /// <summary>Khóa chính tự tăng</summary>
        public long Id { get; set; }

        /// <summary>Thuộc bảng kê chi phí KCB nào</summary>
        public Guid MedicalStatementId { get; set; }

        /// <summary>STT dòng trên bảng kê</summary>
        public int LineOrder { get; set; }

        /// <summary>Mã nhóm mục (I, II, III... hoặc 1, 2, 3...)</summary>
        public string? SectionCode { get; set; }

        /// <summary>Tên nhóm mục (Khám bệnh, Thuốc, CĐHA...)</summary>
        public string? SectionName { get; set; }

        /// <summary>Tên dịch vụ kỹ thuật / thuốc / hóa chất</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Đơn vị tính</summary>
        public string? Unit { get; set; }

        /// <summary>Số lượng</summary>
        public decimal Quantity { get; set; } = 0;

        /// <summary>Đơn giá bệnh viện</summary>
        public decimal UnitPrice { get; set; } = 0;

        /// <summary>Thành tiền chi phí bệnh viện</summary>
        public decimal TotalAmount { get; set; } = 0;

        /// <summary>Quỹ BHYT chi trả</summary>
        public decimal InsurancePaid { get; set; } = 0;

        /// <summary>Người bệnh cùng chi trả trong phạm vi BHYT</summary>
        public decimal PatientCoPay { get; set; } = 0;

        /// <summary>Người bệnh tự trả ngoài phạm vi BHYT</summary>
        public decimal PatientSelfPay { get; set; } = 0;

        /// <summary>Trạng thái giảm trừ: ELIGIBLE | EXCLUDED | NEED_REVIEW</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("deduction_status")]
        public string DeductionStatus { get; set; } = "ELIGIBLE";

        /// <summary>Dòng có hợp lệ để tính giảm trừ thuế TNCN không</summary>
        public bool IsEligibleDeduction { get; set; } = true;

        /// <summary>Lý do không hợp lệ hoặc ghi chú phân loại giảm trừ</summary>
        public string? DeductionReason { get; set; }

        /// <summary>Số tiền được giảm trừ của dòng này</summary>
        public decimal EligibleAmount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public MedicalStatement? MedicalStatement { get; set; }
    }
}
