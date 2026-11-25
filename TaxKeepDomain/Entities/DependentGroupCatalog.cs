using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("dependent_groups")]
    public class DependentGroupCatalog
    {
        [Key]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("relationship_code")]
        [MaxLength(50)]
        public string? RelationshipCode { get; set; }

        [Column("min_age")]
        public int? MinAge { get; set; }

        [Column("max_age")]
        public int? MaxAge { get; set; }

        [Column("require_disabled")]
        public bool RequireDisabled { get; set; } = false;

        [Column("require_studying")]
        public bool RequireStudying { get; set; } = false;

        [Column("level")]
        public int Level { get; set; } = 0;

        [Column("display_order")]
        public int DisplayOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("source")]
        [MaxLength(20)]
        public string Source { get; set; } = "ADMIN"; // SEED | AI | ADMIN

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RelationshipCode")]
        public virtual DependentRelationshipCatalog? Relationship { get; set; }
    }
}
