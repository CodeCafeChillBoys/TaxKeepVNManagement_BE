using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxSettlement;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxSettlementPackageService
    {
        /// <summary>
        /// Sinh tệp PDF Tờ khai Mẫu 02/QTT-TNCN và trả về mảng byte dữ liệu PDF.
        /// </summary>
        Task<(byte[] pdfBytes, string fileName)> GeneratePdfAsync(Guid dossierId, Guid userId, TaxSettlementExportPdfRequest? request = null);

        /// <summary>
        /// Đóng gói đồng bộ toàn bộ hồ sơ gồm Tờ khai PDF và ảnh/PDF chứng từ gốc (Y tế, Giáo dục) vào tệp ZIP.
        /// Lưu trữ tệp ZIP và sinh Signed Download Token có thời hạn sử dụng.
        /// </summary>
        Task<TaxSettlementPackageZipResponse> CreateZipPackageAsync(Guid dossierId, Guid userId, TaxSettlementExportZipRequest? request = null);
    }
}
