using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("dependent_document_rules")]
    public class DependentDocumentRule
    {
        [Key]
        [Column("rule_id")]
        public Guid RuleId { get; set; } = Guid.NewGuid();

        [Column("target_group")]
        [MaxLength(50)]
        public string TargetGroup { get; set; } = string.Empty;

        [Column("doc_type")]
        [MaxLength(50)]
        public string DocType { get; set; } = string.Empty;

        [Column("is_mandatory")]
        public bool IsMandatory { get; set; } = true;

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
