using System;
using System.Collections.Generic;
using TaxKeepVN.Application.DTOs.Wallets;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Thông tin tổng quan Ví Nền Tảng & Sổ cái tài chính của Admin (Admin Platform Wallet & Escrow Ledger Summary)
    /// </summary>
    public class AdminPlatformWalletResponse
    {
        /// <summary>
        /// Tỷ lệ phần trăm hoa hồng nền tảng hiện tại (%) được áp dụng theo chính sách hệ thống
        /// </summary>
        public decimal CurrentCommissionRatePercent { get; set; }

        /// <summary>
        /// ID của ví quỹ hệ thống / doanh thu nền tảng (duy nhất 1 ví dùng chung cho toàn bộ Admin)
        /// </summary>
        public Guid PlatformWalletId { get; set; }

        /// <summary>
        /// Số dư doanh thu hoa hồng khả dụng của nền tảng (tiền lời sàn thực nhận đã kết toán, Admin có thể rút về ngân hàng)
        /// </summary>
        public decimal PlatformAvailableBalance { get; set; }

        /// <summary>
        /// Số dư quỹ nền tảng đang đóng băng trong các lệnh rút tiền đang xử lý
        /// </summary>
        public decimal PlatformFrozenBalance { get; set; }

        /// <summary>
        /// Tổng số tiền hoa hồng / doanh thu nền tảng Admin đã rút về tài khoản ngân hàng thực tế
        /// </summary>
        public decimal PlatformTotalWithdrawn { get; set; }

        /// <summary>
        /// Tổng dòng tiền khách hàng đã thanh toán qua hệ thống (Gross Merchandise Value - GMV)
        /// Gồm các khoản tiền thực tế đã nạp vào tài khoản sàn (HeldInEscrow, CoolingDown, Settled, Refunded)
        /// </summary>
        public decimal TotalGrossVolume { get; set; }

        /// <summary>
        /// Số dư quỹ bảo chứng tạm giữ Escrow hiện tại (Đang nằm trong tài khoản sàn, chờ hoàn tất ca tư vấn)
        /// (EscrowStatus = HeldInEscrow hoặc CoolingDown)
        /// </summary>
        public decimal EscrowHoldingBalance { get; set; }

        /// <summary>
        /// Phần tiền giữ hộ Chuyên gia trong quỹ Escrow (Chờ giải ngân sau khi hoàn tất buổi tư vấn)
        /// </summary>
        public decimal PendingExpertReceivable { get; set; }

        /// <summary>
        /// Phần hoa hồng Sàn đang tạm giữ trong quỹ Escrow (Sẽ ghi nhận doanh thu thực tế khi ca hoàn tất)
        /// </summary>
        public decimal PendingPlatformCommission { get; set; }

        /// <summary>
        /// Doanh thu hoa hồng thực nhận của Nền tảng (Từ các ca đã kết toán Settled)
        /// </summary>
        public decimal RealizedPlatformRevenue { get; set; }

        /// <summary>
        /// Tổng doanh thu hoa hồng toàn bộ (Thực nhận + Đang tạm giữ trong Escrow từ các ca đã thanh toán)
        /// </summary>
        public decimal TotalAccumulatedRevenue { get; set; }

        /// <summary>
        /// Tổng số tiền Sàn đã giải ngân chuyển khoản về ví cho các Chuyên gia tư vấn
        /// </summary>
        public decimal TotalSettledToExperts { get; set; }

        /// <summary>
        /// Tổng số tiền Sàn đã hoàn trả lại cho Người nộp thuế (do hủy hẹn, chuyên gia từ chối, v.v.)
        /// </summary>
        public decimal TotalRefundedToTaxpayers { get; set; }

        /// <summary>
        /// Thống kê số lượng giao dịch theo từng trạng thái
        /// </summary>
        public AdminWalletTransactionStats Stats { get; set; } = new();

        /// <summary>
        /// Lịch sử các biến động số dư của ví hệ thống (hoa hồng từng đơn chảy về, các lần Admin rút tiền)
        /// </summary>
        public List<WalletTransactionResponse> RecentPlatformTransactions { get; set; } = new();

        /// <summary>
        /// Danh sách 10 giao dịch ký quỹ / biến động số dư gần đây nhất
        /// </summary>
        public List<EscrowTransactionResponse> RecentTransactions { get; set; } = new();
    }

    /// <summary>
    /// Thống kê số lượng giao dịch ký quỹ
    /// </summary>
    public class AdminWalletTransactionStats
    {
        public int TotalTransactions { get; set; }
        public int PendingPaymentCount { get; set; }
        public int HeldInEscrowCount { get; set; }
        public int CoolingDownCount { get; set; }
        public int SettledCount { get; set; }
        public int RefundedCount { get; set; }
        public int PartiallyRefundedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
