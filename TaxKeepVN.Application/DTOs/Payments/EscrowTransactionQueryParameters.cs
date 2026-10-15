using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Tham số truy vấn danh sách giao dịch ký quỹ Escrow (Hỗ trợ Filtering, Searching, Sorting, Pagination theo API Design Guidelines)
    /// </summary>
    public class EscrowTransactionQueryParameters
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
        public int PageSize { get => Size; set => Size = value; }

        /// <summary>Tìm kiếm theo BookingCode, OrderCode hoặc PaymentReference</summary>
        public string? Search { get; set; }

        /// <summary>Lọc theo trạng thái dòng tiền Escrow (PendingPayment, HeldInEscrow, CoolingDown, Settled, Refunded...)</summary>
        public EscrowStatus? Status { get; set; }

        /// <summary>Lọc theo mã lịch hẹn cụ thể</summary>
        public Guid? BookingId { get; set; }

        /// <summary>Lọc theo số tiền tối thiểu</summary>
        public decimal? MinAmount { get; set; }

        /// <summary>Lọc theo số tiền tối đa</summary>
        public decimal? MaxAmount { get; set; }

        /// <summary>Lọc từ ngày tạo giao dịch</summary>
        public DateTimeOffset? FromDate { get; set; }

        /// <summary>Lọc đến ngày tạo giao dịch</summary>
        public DateTimeOffset? ToDate { get; set; }

        /// <summary>Sắp xếp: -createdAt (mặc định), createdAt, -amount, amount, -paidAt, paidAt</summary>
        public string? Sort { get; set; } = "-createdAt";
    }
}
