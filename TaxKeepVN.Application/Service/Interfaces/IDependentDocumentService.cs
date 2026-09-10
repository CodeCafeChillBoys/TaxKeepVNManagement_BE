using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentDocumentService
    {
        Task<DependentDocument> UploadDocumentAsync(Guid userId, Guid dependentId, string docTypeString, IFormFile file);
    }
}
