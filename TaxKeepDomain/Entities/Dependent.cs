using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    public class Dependent
    {
        public Guid Id { get; set; }
        public Guid TaxpayerId { get; set; } // Map to User.Id
        public string FullName { get; set; } = string.Empty;
        
        // Navigation property
        public ICollection<DependentDocument> Documents { get; set; } = new List<DependentDocument>();
    }
}
