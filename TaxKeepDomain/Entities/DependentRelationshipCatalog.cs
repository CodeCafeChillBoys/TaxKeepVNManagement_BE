using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("dependent_relationships")]
    public class DependentRelationshipCatalog
    {
        [Key]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<DependentGroupCatalog> Groups { get; set; } = new List<DependentGroupCatalog>();
    }
}
