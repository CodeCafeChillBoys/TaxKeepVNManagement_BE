using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Catalog
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DependentCatalogResponseDto
    {
        public List<DependentRelationshipItemDto> Relationships { get; set; } = new();
        public List<DependentGroupItemDto> Groups { get; set; } = new();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DependentRelationshipItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DependentGroupItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RelationshipCode { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool RequireDisabled { get; set; }
        public bool RequireStudying { get; set; }
        public int Level { get; set; }
        public int DisplayOrder { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class AdminDependentGroupDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RelationshipCode { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool RequireDisabled { get; set; }
        public bool RequireStudying { get; set; }
        public int Level { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public string Source { get; set; } = "ADMIN";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class CreateDependentGroupRequest
    {
        [Required(ErrorMessage = "Mã nhóm (code) không được để trống.")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên nhóm (name) không được để trống.")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? RelationshipCode { get; set; }

        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool RequireDisabled { get; set; } = false;
        public bool RequireStudying { get; set; } = false;
        public int Level { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class AdminUpdateDependentGroupRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? RelationshipCode { get; set; }

        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool? RequireDisabled { get; set; }
        public bool? RequireStudying { get; set; }
        public int? Level { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class ActivateAiGroupRequest
    {
        [Required(ErrorMessage = "Bắt buộc phải chọn nhóm quan hệ (relationshipCode) khi kích hoạt nhóm do AI gợi ý.")]
        [MaxLength(50)]
        public string RelationshipCode { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Name { get; set; }

        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool? RequireDisabled { get; set; }
        public bool? RequireStudying { get; set; }
        public int? Level { get; set; }
        public int? DisplayOrder { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class AdminDependentRelationshipDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class CreateDependentRelationshipRequest
    {
        [Required(ErrorMessage = "Mã quan hệ (code) không được để trống.")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên quan hệ (name) không được để trống.")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UpdateDependentRelationshipRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}
