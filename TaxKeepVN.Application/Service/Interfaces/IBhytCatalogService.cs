using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.MedicalClaims;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IBhytCatalogService
    {
        Task<List<BhytCatalogItemDto>> GetCatalogAsync(string? type, string? search, bool? isActive);
        Task<BhytCatalogItemDto> GetByIdAsync(Guid id);
        Task<BhytCatalogItemDto> CreateItemAsync(CreateBhytCatalogItemRequest request);
        Task<BhytCatalogItemDto> UpdateItemAsync(Guid id, UpdateBhytCatalogItemRequest request);
        Task DeleteItemAsync(Guid id);
    }
}
