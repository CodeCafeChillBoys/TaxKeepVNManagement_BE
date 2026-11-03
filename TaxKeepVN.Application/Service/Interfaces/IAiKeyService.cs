using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IAiKeyService
    {
        Task<List<AiKeyResponseDto>> GetAllKeysAsync();
        Task<AiKeyResponseDto> CreateKeyAsync(AiKeyCreateRequestDto request);
        Task<AiKeyResponseDto?> UpdateKeyAsync(Guid id, AiKeyUpdateRequestDto request);
        Task<bool> DeleteKeyAsync(Guid id);
    }
}
