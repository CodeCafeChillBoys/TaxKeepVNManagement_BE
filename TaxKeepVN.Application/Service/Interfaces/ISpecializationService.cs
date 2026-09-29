using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Specializations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ISpecializationService
    {
        Task<IEnumerable<SpecializationDto>> GetAllAsync(SpecializationQueryParameters? query = null);
        Task<SpecializationDto> GetByIdAsync(Guid id);
        Task<SpecializationDto> GetByCodeAsync(string code);
        Task<SpecializationDto> CreateAsync(CreateSpecializationDto dto);
        Task<SpecializationDto> UpdateAsync(Guid id, UpdateSpecializationDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
