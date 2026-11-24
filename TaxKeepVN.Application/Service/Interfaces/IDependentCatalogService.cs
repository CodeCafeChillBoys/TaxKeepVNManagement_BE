using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Catalog;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentCatalogService
    {
        /// <summary>
        /// Lấy danh mục quan hệ và nhóm người phụ thuộc đang kích hoạt (active) cho người dùng.
        /// Sử dụng cache (MemoryCache + Redis).
        /// </summary>
        Task<DependentCatalogResponseDto> GetActiveCatalogAsync();

        // Admin Groups CRUD
        Task<List<AdminDependentGroupDto>> GetAllGroupsAsync(bool? isActive = null);
        Task<AdminDependentGroupDto> GetGroupByCodeAsync(string code);
        Task<AdminDependentGroupDto> CreateGroupAsync(CreateDependentGroupRequest request);
        Task<AdminDependentGroupDto> UpdateGroupAsync(string code, UpdateDependentGroupRequest request);
        Task DeleteGroupAsync(string code);
        Task<AdminDependentGroupDto> ActivateAiGroupAsync(string code, ActivateAiGroupRequest request);

        // Admin Relationships CRUD
        Task<List<AdminDependentRelationshipDto>> GetAllRelationshipsAsync(bool? isActive = null);
        Task<AdminDependentRelationshipDto> GetRelationshipByCodeAsync(string code);
        Task<AdminDependentRelationshipDto> CreateRelationshipAsync(CreateDependentRelationshipRequest request);
        Task<AdminDependentRelationshipDto> UpdateRelationshipAsync(string code, UpdateDependentRelationshipRequest request);
        Task DeleteRelationshipAsync(string code);
    }
}
