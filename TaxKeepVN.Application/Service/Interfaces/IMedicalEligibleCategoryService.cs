using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.MedicalCategories;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IMedicalEligibleCategoryService
    {
        Task<IEnumerable<MedicalEligibleCategoryDto>> GetAllAsync(MedicalCategoryQueryParameters? query = null);
        Task<MedicalEligibleCategoryDto> GetByIdAsync(Guid id);
        Task<MedicalEligibleCategoryDto> CreateAsync(CreateMedicalEligibleCategoryDto dto);
        Task<MedicalEligibleCategoryDto> UpdateAsync(Guid id, UpdateMedicalEligibleCategoryDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> RestoreAsync(Guid id);
    }
}
