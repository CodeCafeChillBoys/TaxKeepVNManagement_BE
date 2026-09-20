using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.SystemConfig;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxAiConfigService
    {
        Task<List<SystemConfigResponseDto>> GetAllConfigsAsync(bool activeOnly = false);
        Task<SystemConfigResponseDto?> GetConfigByKeyAsync(string key);
        Task<SystemConfigResponseDto> CreateConfigAsync(SystemConfigCreateRequestDto request);
        Task<SystemConfigResponseDto> UpdateConfigAsync(string key, SystemConfigUpdateRequestDto request);
        Task<bool> DeleteConfigAsync(string key, Guid? adminId = null);
        Task<SystemConfigResponseDto> RestoreConfigAsync(string key, Guid? adminId = null);
        Task<ThresholdResolveTestResponseDto> TestResolveThresholdAsync(string? categoryCode = null);
    }
}