using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxSettlement;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxSettlementPdfService
    {
        /// <summary>
        /// Chuẩn bị toàn bộ dữ liệu (Người nộp thuế, Số liệu quyết toán, Bảng bậc thuế, Phụ lục NPT, Phụ lục Y tế/Giáo dục)
        /// để nạp vào template vẽ PDF.
        /// </summary>
        Task<TaxSettlementPdfModel> BuildPdfModelAsync(Guid dossierId, Guid userId, TaxSettlementExportPdfRequest? request = null);

        /// <summary>
        /// Sinh mảng byte PDF Tờ khai Mẫu 02/QTT-TNCN và các phụ lục bằng thư viện QuestPDF.
        /// </summary>
        byte[] GenerateSettlementPdf(TaxSettlementPdfModel model);
    }
}
