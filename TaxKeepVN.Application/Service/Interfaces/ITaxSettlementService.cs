using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxSettlement;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxSettlementService
    {
        /// <summary>
        /// Bước 1-3 sơ đồ: Gom dữ liệu, tính toán nháp, trả về xem trước toàn bộ số liệu.
        /// Không lưu vào DB — chỉ tính và trả về.
        /// </summary>
        Task<TaxSettlementPreviewDto> PreviewAsync(Guid taxpayerId, TaxSettlementPreviewRequest request);

        /// <summary>
        /// Bước 4-5 sơ đồ: Chốt danh sách chứng từ, tính toán chính thức, lưu vào DB (LOCKED).
        /// </summary>
        Task<TaxSettlementPreviewDto> ExportAsync(Guid taxpayerId, TaxSettlementExportRequest request);

        /// <summary>Lấy danh sách hồ sơ quyết toán đã tạo của người dùng.</summary>
        Task<List<TaxSettlementListItemDto>> GetListAsync(Guid taxpayerId);

        /// <summary>Lấy chi tiết một hồ sơ quyết toán.</summary>
        Task<TaxSettlementPreviewDto> GetByIdAsync(Guid dossierId, Guid taxpayerId);
    }
}
