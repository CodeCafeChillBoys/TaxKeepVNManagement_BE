using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.TaxPeriods
{
    public class InitTaxPeriodRequest
    {
        [Required(ErrorMessage = "The userId field is required.")]
        public Guid? UserId { get; set; }
        [Required(ErrorMessage = "The taxYear field is required.")]
        public int? TaxYear { get; set; }
    }
}