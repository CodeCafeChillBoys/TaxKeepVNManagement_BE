using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IPromptTemplateService
    {
        Task<List<PromptTemplateResponseDto>> GetAllPromptsAsync(bool activeOnly = false);
        Task<PromptTemplateResponseDto?> GetPromptByKeyAsync(string promptKey);
        Task<PromptTemplateResponseDto> CreatePromptAsync(PromptTemplateCreateRequestDto request);
        Task<PromptTemplateResponseDto> UpdatePromptAsync(string promptKey, PromptTemplateUpdateRequestDto request);
        Task<bool> DeletePromptAsync(string promptKey);
        Task<PromptTestRenderResponseDto> TestRenderPromptAsync(PromptTestRenderRequestDto request);
    }
}
