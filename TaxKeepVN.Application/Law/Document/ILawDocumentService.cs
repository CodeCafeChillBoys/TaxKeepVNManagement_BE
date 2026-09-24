using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaxKeepVN.Application.DTOs.Law;

namespace TaxKeepVN.Application.Law.Document
{
    public interface ILawDocumentService
    {
        Task<UploadDocumentResponseDto> UploadDocumentAsync(IFormFile file, string? sourceUrl, string? documentNumberHint, Guid adminId, string baseUrl, CancellationToken ct = default);
        Task<List<LegalDocumentDto>> GetDocumentsAsync(string? status, string? q, int page = 1, int size = 20, CancellationToken ct = default);
        Task<LegalDocumentDetailDto> GetDocumentDetailAsync(Guid id, CancellationToken ct = default);
        Task<LegalDocumentDto> UpdateDocumentAsync(Guid id, UpdateLegalDocumentInput input, CancellationToken ct = default);
    }
}
