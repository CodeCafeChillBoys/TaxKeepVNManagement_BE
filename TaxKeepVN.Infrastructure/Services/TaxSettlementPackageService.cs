using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Infrastructure.Services
{
    public class TaxSettlementPackageService : ITaxSettlementPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITaxSettlementPdfService _pdfService;
        private readonly IDownloadTokenService _downloadTokenService;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TaxSettlementPackageService> _logger;

        public TaxSettlementPackageService(
            IUnitOfWork unitOfWork,
            ITaxSettlementPdfService pdfService,
            IDownloadTokenService downloadTokenService,
            IWebHostEnvironment env,
            IHttpClientFactory httpClientFactory,
            ILogger<TaxSettlementPackageService> logger)
        {
            _unitOfWork = unitOfWork;
            _pdfService = pdfService;
            _downloadTokenService = downloadTokenService;
            _env = env;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(byte[] pdfBytes, string fileName)> GeneratePdfAsync(
            Guid dossierId, Guid userId, TaxSettlementExportPdfRequest? request = null)
        {
            var model = await _pdfService.BuildPdfModelAsync(dossierId, userId, request);
            var pdfBytes = _pdfService.GenerateSettlementPdf(model);

            var codeOrId = !string.IsNullOrWhiteSpace(model.TaxCode) ? model.TaxCode : userId.ToString()[..8];
            var fileName = $"ToKhai_02_QTT_TNCN_{model.TaxYear}_{codeOrId}.pdf";

            return (pdfBytes, fileName);
        }

        public async Task<TaxSettlementPackageZipResponse> CreateZipPackageAsync(
            Guid dossierId, Guid userId, TaxSettlementExportZipRequest? request = null)
        {
            var dossierRepo = _unitOfWork.Repository<TaxSettlementDossier>();
            var dossier = await dossierRepo.GetByIdAsync(dossierId)
                ?? throw new NotFoundException($"Không tìm thấy hồ sơ quyết toán '{dossierId}'.");

            if (dossier.TaxpayerId != userId)
                throw new ForbiddenException("Bạn không có quyền truy cập hồ sơ quyết toán này.");

            var taxYear = dossier.TaxYear;

            // 1. Sinh Tờ khai PDF Mẫu 02/QTT-TNCN và Phụ lục bằng QuestPDF
            var (pdfBytes, pdfFileName) = await GeneratePdfAsync(dossierId, userId, request);

            // 2. Truy vấn danh sách chứng từ y tế, giáo dục đã xác nhận (CONFIRMED)
            var docRepo = _unitOfWork.Repository<Document>();
            var eligibleDocs = (await docRepo.FindAsync(d =>
                d.Period != null &&
                d.Period.UserId == userId &&
                d.Period.TaxYear == (short)taxYear &&
                d.Status == "CONFIRMED" &&
                (d.DocTypeCode == "MEDICAL_RECEIPT" || d.DocTypeCode == "EDUCATION_RECEIPT"))).ToList();

            // 3. Đóng gói toàn bộ vào luồng nén ZIP
            using var memoryStream = new MemoryStream();
            int documentsIncluded = 0;

            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                // Thêm tệp Tờ khai PDF vào thư mục gốc của file ZIP
                var pdfEntry = archive.CreateEntry("ToKhai_02_QTT_TNCN.pdf", CompressionLevel.Optimal);
                using (var entryStream = pdfEntry.Open())
                {
                    await entryStream.WriteAsync(pdfBytes);
                }

                // Thêm các chứng từ gốc vào các thư mục con tương ứng
                foreach (var doc in eligibleDocs)
                {
                    if (string.IsNullOrWhiteSpace(doc.FileUrl)) continue;

                    var subFolder = doc.DocTypeCode == "MEDICAL_RECEIPT"
                        ? "ChungTu_GiamTru/YTe/"
                        : "ChungTu_GiamTru/GiaoDuc/";

                    var cleanDocNumber = SanitizeFileName(doc.InvoiceNumber ?? doc.Id.ToString()[..8]);
                    var ext = Path.GetExtension(doc.OriginalFilename ?? doc.FileUrl);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                    var entryName = $"{subFolder}CT_{cleanDocNumber}{ext}";

                    var fileBytes = await TryFetchFileBytesAsync(doc.FileUrl);
                    if (fileBytes != null && fileBytes.Length > 0)
                    {
                        var docEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var docStream = docEntry.Open();
                        await docStream.WriteAsync(fileBytes);
                        documentsIncluded++;
                    }
                    else
                    {
                        // Thêm file text ghi chú nếu file ảnh bị thất lạc
                        var noteEntry = archive.CreateEntry($"{subFolder}GhiChu_{cleanDocNumber}.txt", CompressionLevel.Fastest);
                        using var noteWriter = new StreamWriter(noteEntry.Open());
                        await noteWriter.WriteLineAsync($"Số hóa đơn: {doc.InvoiceNumber}");
                        await noteWriter.WriteLineAsync($"Đơn vị phát hành: {doc.SellerName}");
                        await noteWriter.WriteLineAsync($"Số tiền: {doc.TotalAmount:N0} VNĐ");
                        await noteWriter.WriteLineAsync($"File URL: {doc.FileUrl}");
                    }
                }
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            var zipBytes = memoryStream.ToArray();

            // 4. Lưu tệp ZIP vật lý vào ổ đĩa wwwroot/uploads/tax-settlements/{taxYear}/
            var folderRelative = Path.Combine("uploads", "tax-settlements", taxYear.ToString());
            var folderAbsolute = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), folderRelative);

            if (!Directory.Exists(folderAbsolute))
            {
                Directory.CreateDirectory(folderAbsolute);
            }

            var safeId = dossier.Id.ToString();
            var zipFileName = $"HoSoQuyetToan_TNCN_{taxYear}_{safeId[..8].ToUpper()}.zip";
            var fileAbsolute = Path.Combine(folderAbsolute, zipFileName);
            var relativeUrl = $"/{folderRelative.Replace('\\', '/')}/{zipFileName}";

            await File.WriteAllBytesAsync(fileAbsolute, zipBytes);

            // 5. Cập nhật đường dẫn vào hồ sơ
            dossier.ZipFileUrl = relativeUrl;
            if (!string.IsNullOrWhiteSpace(request?.Note))
            {
                dossier.Note = request.Note;
            }
            await _unitOfWork.SaveChangesAsync();

            // 6. Sinh Signed Download Token có thời hạn 30 phút
            var tokenPayload = new DownloadTokenPayload
            {
                DossierId = dossier.Id,
                TaxpayerId = userId,
                FileType = "zip",
                RelativeFilePath = relativeUrl,
                DownloadFileName = zipFileName
            };

            var validity = TimeSpan.FromMinutes(30);
            var token = _downloadTokenService.GenerateToken(tokenPayload, validity);
            var downloadUrl = $"/api/v1/tax-settlements/download/{token}";

            _logger.LogInformation("Đã đóng gói thành công tệp ZIP cho hồ sơ '{DossierId}' ({Bytes} bytes, {Docs} chứng từ).",
                dossierId, zipBytes.Length, documentsIncluded);

            return new TaxSettlementPackageZipResponse
            {
                DossierId = dossier.Id,
                FileName = zipFileName,
                DownloadUrl = downloadUrl,
                ExpiresAt = DateTime.UtcNow.Add(validity),
                FileSizeBytes = zipBytes.Length,
                TotalDocumentsIncluded = documentsIncluded,
                Message = $"Đóng gói hồ sơ quyết toán thành công ({documentsIncluded} chứng từ đính kèm). Đường dẫn tải có hiệu lực trong 30 phút."
            };
        }

        private async Task<byte[]?> TryFetchFileBytesAsync(string fileUrl)
        {
            try
            {
                // Trường hợp 1: File URL dạng local path (/uploads/...)
                if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
                    fileUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    var cleanRel = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var absPath = Path.Combine(rootPath, cleanRel);

                    if (File.Exists(absPath))
                    {
                        return await File.ReadAllBytesAsync(absPath);
                    }
                }

                // Trường hợp 2: File URL dạng remote (Supabase hoặc Cloud Storage)
                if (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    return await client.GetByteArrayAsync(fileUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc dữ liệu file từ URL '{Url}': {Message}", fileUrl, ex.Message);
            }

            return null;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return Regex.Replace(clean, @"\s+", "_");
        }
    }
}
