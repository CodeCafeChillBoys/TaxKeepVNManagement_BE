using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxDocumentTypes;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxDocumentTypeService
    {
        Task<IEnumerable<TaxDocumentTypeDto>> GetAllAsync(TaxDocumentTypeQueryParameters? query = null);
        Task<TaxDocumentTypeDto> GetByCodeAsync(string code);
        Task<TaxDocumentTypeDto> CreateAsync(CreateTaxDocumentTypeDto dto);
        Task<TaxDocumentTypeDto> UpdateAsync(string code, UpdateTaxDocumentTypeDto dto);
        Task<TaxDocumentType> EnsureExistsAsync(string code, string? defaultName = null);
    }
}
