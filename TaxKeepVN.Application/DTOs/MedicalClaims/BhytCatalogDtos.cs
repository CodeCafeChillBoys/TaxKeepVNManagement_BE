using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.MedicalClaims
{
    public class BhytCatalogItemDto
    {
        public Guid Id { get; set; }
        public string CatalogType { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public string? ActiveIngredient { get; set; }
        public string? LegalDocumentRef { get; set; }
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateBhytCatalogItemRequest
    {
        [Required]
        [MaxLength(50)]
        public string CatalogType { get; set; } = string.Empty; // DRUG | SERVICE | SUPPLY

        [MaxLength(100)]
        public string? Code { get; set; }

        [Required]
        [MaxLength(500)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ActiveIngredient { get; set; }

        [MaxLength(255)]
        public string? LegalDocumentRef { get; set; }

        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
    }

    public class UpdateBhytCatalogItemRequest
    {
        [Required]
        [MaxLength(50)]
        public string CatalogType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Code { get; set; }

        [Required]
        [MaxLength(500)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ActiveIngredient { get; set; }

        [MaxLength(255)]
        public string? LegalDocumentRef { get; set; }

        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
