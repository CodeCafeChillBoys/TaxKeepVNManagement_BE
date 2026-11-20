using System;

namespace TaxKeepVN.Domain.Entities
{
    public class DocumentItem
    {
        /// <summary>Khóa chính tự tăng</summary>
        public long Id { get; set; }

        /// <summary>Thuộc hóa đơn nào</summary>
        public Guid DocumentId { get; set; }

        /// <summary>STT dòng (1, 2...)</summary>
        public int ItemOrder { get; set; }

        /// <summary>Tên hàng hóa/dịch vụ</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Đơn vị tính (Lần, cái...)</summary>
        public string? Unit { get; set; }

        /// <summary>Số lượng (1, 4...)</summary>
        public decimal Quantity { get; set; } = 0;

        /// <summary>Đơn giá</summary>
        public decimal UnitPrice { get; set; } = 0;

        /// <summary>Thành tiền</summary>
        public decimal TotalPrice { get; set; } = 0;

        // ── Chi tiết chi phí KCB theo 13 cột QĐ 697/QĐ-BYT (Mẫu 01/KBCB) ──────────
        /// <summary>Số thứ tự khối thẻ / mức hưởng trong bảng kê</summary>
        public int? SectionIndex { get; set; }

        /// <summary>Khối này có thẻ BHYT hay không</summary>
        public bool? HasInsuranceCard { get; set; }

        /// <summary>Mức hưởng BHYT của khối</summary>
        public decimal? BenefitRate { get; set; }

        /// <summary>Mã mục cố định (1..12: 1 Khám bệnh, 2 Ngày giường, 3 XN, 4 CĐHA, 8 Thuốc...)</summary>
        public string? GroupCode { get; set; }

        /// <summary>Tên mục như in trên bảng kê</summary>
        public string? GroupName { get; set; }

        /// <summary>Là dòng con trong gói thiết bị y tế (Mục 10.x), tiền tính ở dòng cha</summary>
        public bool IsPackageSubLine { get; set; } = false;

        /// <summary>Cột (5) Đơn giá BH</summary>
        public decimal? UnitPriceInsurance { get; set; }

        /// <summary>Cột (6) Tỷ lệ thanh toán theo dịch vụ (%)</summary>
        public decimal? ServiceRate { get; set; }

        /// <summary>Cột (7) Thành tiền BV</summary>
        public decimal? HospitalAmount { get; set; }

        /// <summary>Cột (8) Tỷ lệ thanh toán BHYT (%) - Nếu có thẻ và tỷ lệ = 0% là ngoài danh mục</summary>
        public decimal? InsuranceRate { get; set; }

        /// <summary>Cột (9) Thành tiền BH</summary>
        public decimal? InsuranceAmount { get; set; }

        /// <summary>Cột (10) Quỹ BHYT thanh toán</summary>
        public decimal? InsuranceFundPaid { get; set; }

        /// <summary>Cột (11) Người bệnh cùng chi trả (khoản đủ điều kiện giảm trừ nếu có thẻ)</summary>
        public decimal? PatientCoPay { get; set; }

        /// <summary>Cột (12) Nguồn khác thanh toán</summary>
        public decimal? OtherSourcePaid { get; set; }

        /// <summary>Cột (13) Người bệnh tự trả (ngoài danh mục / chênh lệch giá / ngoài tỷ lệ)</summary>
        public decimal? PatientSelfPay { get; set; }

        /// <summary>Trạng thái giảm trừ dòng: ELIGIBLE, EXCLUDED, NEED_REVIEW</summary>
        public string? DeductionStatus { get; set; }

        /// <summary>Mã lý do giảm trừ (IN_COVERAGE_COPAY, OUT_OF_COVERAGE, PRICE_DIFFERENCE_SELF_PAY...)</summary>
        public string? DeductionReason { get; set; }

        /// <summary>Số tiền của dòng này đủ điều kiện giảm trừ thuế</summary>
        public decimal? EligibleAmount { get; set; }

        /// <summary>Phần người bệnh tự trả K2/K3 hoặc vượt giá BH của dòng không thẻ</summary>
        public decimal? SelfPayAmount { get; set; }

        /// <summary>Lý do phần tự trả: PRICE_DIFFERENCE_SELF_PAY, PARTIAL_RATE_SELF_PAY</summary>
        public string? SelfPayReason { get; set; }

        /// <summary>Người dùng chủ động chọn tính cả phần tự trả vào giảm trừ (mặc định false)</summary>
        public bool SelfPayOptIn { get; set; } = false;

        /// <summary>Người dùng đã can thiệp ghi đè trạng thái dòng trên giao diện duyệt</summary>
        public bool IsUserOverridden { get; set; } = false;

        // Navigation property
        public Document? Document { get; set; }
    }
}
