using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Specializations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ISpecializationService
    {
        Task<IEnumerable<SpecializationDto>> GetAllAsync(SpecializationQueryParameters? query = null);
        Task<SpecializationDto> GetByIdAsync(int id);
        Task<SpecializationDto> GetByCodeAsync(string code);
        Task<SpecializationDto> CreateAsync(CreateSpecializationDto dto);
        Task<SpecializationDto> UpdateAsync(int id, UpdateSpecializationDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
