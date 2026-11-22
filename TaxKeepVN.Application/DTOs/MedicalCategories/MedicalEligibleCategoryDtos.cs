using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.MedicalCategories
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MedicalEligibleCategoryDto
    {
        public Guid Id { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SectionCode { get; set; } = string.Empty;
        public bool IsEligible { get; set; } = true;
        public string? Description { get; set; }
        public string? Keywords { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class CreateMedicalEligibleCategoryDto
    {
        [Required(ErrorMessage = "Mã danh mục chi phí không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã danh mục chi phí phải từ 2 đến 50 ký tự.")]
        public string CategoryCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên danh mục chi phí không được để trống.")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Tên danh mục chi phí phải từ 2 đến 255 ký tự.")]
        public string CategoryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã mục theo QĐ 697 (VD: I, II, III...) không được để trống.")]
        [StringLength(20, ErrorMessage = "Mã mục tối đa 20 ký tự.")]
        public string SectionCode { get; set; } = string.Empty;

        public bool IsEligible { get; set; } = true;

        public string? Description { get; set; }

        public string? Keywords { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UpdateMedicalEligibleCategoryDto
    {
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Tên danh mục chi phí phải từ 2 đến 255 ký tự.")]
        public string? CategoryName { get; set; }

        [StringLength(20, ErrorMessage = "Mã mục tối đa 20 ký tự.")]
        public string? SectionCode { get; set; }

        public bool? IsEligible { get; set; }

        public string? Description { get; set; }

        public string? Keywords { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MedicalCategoryQueryParameters
    {
        public string? Search { get; set; }
        public string? SectionCode { get; set; }
        public bool? IsEligible { get; set; }
        public bool IncludeDeleted { get; set; } = false;
    }
}
