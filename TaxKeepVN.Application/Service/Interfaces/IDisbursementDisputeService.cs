using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Disputes;
using TaxKeepVN.Application.DTOs.Wallets;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện Service xử lý Giải ngân thù lao, Ví điện tử, Rút tiền và Xử lý tranh chấp (Đặc tả 7)
    /// </summary>
    public interface IDisbursementDisputeService
    {
        // ── 1. GIẢI NGÂN & XÁC NHẬN HÀI LÒNG SỚM ──────────────────────────────────

        /// <summary>
        /// Khách hàng chủ động bấm "Xác nhận hài lòng" sớm trong thời gian 24 giờ Cool-down
        /// Hệ thống lập tức giải ngân thù lao vào ví chuyên gia và đóng ca (SETTLED)
        /// </summary>
        Task ConfirmSatisfactionAsync(Guid userId, Guid bookingId);

        /// <summary>
        /// Hệ thống (Background Job) tự động giải ngân cho ca tư vấn đã hoàn thành sau khi hết thời gian Cool-down 24 giờ mà không có khiếu nại
        /// </summary>
        Task<bool> ProcessCoolDownDisbursementAsync(Guid bookingId);

        // ── 2. KHIẾU NẠI & TRANH CHẤP DỊCH VỤ ────────────────────────────────────

        /// <summary>
        /// Kiểm tra tính hợp lệ và thời hạn gửi khiếu nại của khách hàng (trong vòng 24 giờ kể từ khi hết ca)
        /// </summary>
        Task<DisputeEligibilityResponse> CheckDisputeEligibilityAsync(Guid userId, Guid bookingId);

        /// <summary>
        /// Khách hàng gửi khiếu nại chất lượng dịch vụ tư vấn trong vòng 24 giờ Cool-down
        /// Hệ thống lập tức đóng băng tiền Escrow và chuyển Booking sang trạng thái UNDER_DISPUTE
        /// </summary>
        Task<DisputeDetailResponse> SubmitDisputeAsync(Guid userId, Guid bookingId, CreateDisputeRequest request);

        /// <summary>
        /// Chuyên gia gửi phản hồi / bằng chứng giải trình khiếu nại (thời hạn 24 giờ)
        /// </summary>
        Task<DisputeDetailResponse> ExpertRespondDisputeAsync(Guid expertUserId, Guid disputeId, ExpertRespondDisputeRequest request);

        /// <summary>
        /// Xem chi tiết một vụ việc khiếu nại (dành cho Khách hàng, Chuyên gia hoặc Admin)
        /// </summary>
        Task<DisputeDetailResponse> GetDisputeByIdAsync(Guid currentUserId, string currentUserRole, Guid disputeId);

        /// <summary>
        /// Lấy thông tin khiếu nại gắn liền với một Booking
        /// </summary>
        Task<DisputeDetailResponse?> GetDisputeByBookingIdAsync(Guid currentUserId, string currentUserRole, Guid bookingId);

        /// <summary>
        /// Lấy danh sách khiếu nại (Admin xem toàn bộ; Khách hàng/Chuyên gia chỉ xem ca của mình)
        /// </summary>
        Task<PagedResult<DisputeListItemResponse>> GetDisputesAsync(Guid currentUserId, string currentUserRole, string? status = null, int page = 1, int size = 10);

        /// <summary>
        /// Admin đưa ra phán quyết cuối cùng giải quyết tranh chấp và thực thi thanh toán tài chính
        /// </summary>
        Task<DisputeDetailResponse> AdminResolveDisputeAsync(Guid adminId, Guid disputeId, AdminResolveDisputeRequest request);

        // ── 3. QUẢN LÝ VÍ & LỆNH RÚT TIỀN ────────────────────────────────────────

        /// <summary>
        /// Xem thông tin ví cá nhân và 10 giao dịch biến động số dư gần nhất
        /// </summary>
        Task<WalletResponse> GetMyWalletAsync(Guid userId, string? walletType = null);

        /// <summary>
        /// Lấy lịch sử biến động số dư ví (phân trang)
        /// </summary>
        Task<PagedResult<WalletTransactionResponse>> GetMyWalletTransactionsAsync(Guid userId, int page = 1, int size = 10);

        /// <summary>
        /// Chuyên gia tạo lệnh rút tiền từ Ví thu nhập về tài khoản ngân hàng cá nhân
        /// Yêu cầu: Đã hoàn tất eKYC (IsVerified), có Mã số thuế cá nhân (TaxIdNumber), số tiền >= EXPERT_MIN_WITHDRAWAL_VND
        /// </summary>
        Task<WithdrawalResponse> RequestWithdrawalAsync(Guid expertUserId, CreateWithdrawalRequest request);

        /// <summary>
        /// Khách hàng tạo lệnh rút tiền hoàn (Refund) từ Ví cá nhân về tài khoản ngân hàng
        /// </summary>
        Task<WithdrawalResponse> RequestUserRefundWithdrawalAsync(Guid userId, CreateWithdrawalRequest request);

        /// <summary>
        /// Lấy danh sách lệnh rút tiền (Admin xem toàn bộ; Chuyên gia chỉ xem của mình)
        /// </summary>
        Task<PagedResult<WithdrawalResponse>> GetWithdrawalsAsync(Guid currentUserId, string currentUserRole, string? status = null, int page = 1, int size = 10);

        /// <summary>
        /// Admin xét duyệt hoặc từ chối lệnh rút tiền của chuyên gia
        /// </summary>
        Task<WithdrawalResponse> AdminProcessWithdrawalAsync(Guid adminId, Guid withdrawalId, AdminProcessWithdrawalRequest request);
    }
}
