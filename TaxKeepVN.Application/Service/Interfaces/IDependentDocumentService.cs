using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentDocumentService
    {
        Task<UploadDocumentResultDto> UploadDocumentAsync(Guid userId, Guid dependentId, string docTypeString, IFormFile file);
    }
}
