using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Documents
{
    public class DocumentReviewResponseDto
    {
        public Guid Id { get; set; }
        public Guid PeriodId { get; set; }
        public string? DocTypeCode { get; set; }
        public string? DocTypeName { get; set; }
        public bool? IsTaxEligible { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? OriginalFilename { get; set; }
        public string? InvoiceSeries { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? SellerName { get; set; }
        public string? SellerTaxCode { get; set; }
        public string? SellerAddress { get; set; }
        public string? SellerPhone { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerTaxCode { get; set; }
        public string? BuyerIdCard { get; set; }
        public string? BuyerAddress { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? TotalAmountInWords { get; set; }
        public string? LookupUrl { get; set; }
        public string? LookupCode { get; set; }
        public short? ExtractedYear { get; set; }
        public bool? IsYearValid { get; set; }
        public bool? IsIdentityValid { get; set; }
        public bool? IsNotReimbursed { get; set; }
        public string Status { get; set; } = "CONFIRMED";
        public DateTime CreatedAt { get; set; }
        public List<DocumentItemResponseDto> Items { get; set; } = new();
    }

    public class DocumentItemResponseDto
    {
        public long Id { get; set; }
        public int ItemOrder { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
    }
}
