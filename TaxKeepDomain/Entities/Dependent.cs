using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    public class Dependent
    {
        public Guid Id { get; set; }
        public Guid TaxpayerId { get; set; } // Map to User.Id
        public string FullName { get; set; } = string.Empty;
        
        public DateTime BirthDate { get; set; }
        public DependentGroup CurrentGroup { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsProfileComplete { get; set; } // Added for profile completion tracking

        // Navigation property
        public ICollection<DependentDocument> Documents { get; set; } = new List<DependentDocument>();
    }
}
