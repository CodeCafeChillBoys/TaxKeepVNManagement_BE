using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("medical_eligible_categories")]
    public class MedicalEligibleCategory
    {
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("category_code")]
        public string CategoryCode { get; set; } = string.Empty;

        [Column("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        [Column("section_code")]
        public string SectionCode { get; set; } = string.Empty;

        [Column("is_eligible")]
        public bool IsEligible { get; set; } = true;

        [Column("description")]
        public string? Description { get; set; }

        [Column("keywords")]
        public string? Keywords { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
