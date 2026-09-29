using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class DocumentOcrResponseMessage
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public DocumentOcrData? Data { get; set; }
    }

    public class DocumentOcrData
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("periodId")]
        public Guid? PeriodId { get; set; }

        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("docTypeCode")]
        public string? DocTypeCode { get; set; }

        [JsonPropertyName("fileUrl")]
        public string? FileUrl { get; set; }

        [JsonPropertyName("originalFilename")]
        public string? OriginalFilename { get; set; }

        [JsonPropertyName("invoiceSeries")]
        public string? InvoiceSeries { get; set; }

        [JsonPropertyName("invoiceNumber")]
        public string? InvoiceNumber { get; set; }

        [JsonPropertyName("invoiceDate")]
        public string? InvoiceDate { get; set; }

        [JsonPropertyName("extractedYear")]
        public int? ExtractedYear { get; set; }

        [JsonPropertyName("sellerName")]
        public string? SellerName { get; set; }

        [JsonPropertyName("sellerTaxCode")]
        public string? SellerTaxCode { get; set; }

        [JsonPropertyName("sellerAddress")]
        public string? SellerAddress { get; set; }

        [JsonPropertyName("sellerPhone")]
        public string? SellerPhone { get; set; }

        [JsonPropertyName("buyerName")]
        public string? BuyerName { get; set; }

        [JsonPropertyName("buyerTaxCode")]
        public string? BuyerTaxCode { get; set; }

        [JsonPropertyName("buyerIdCard")]
        public string? BuyerIdCard { get; set; }

        [JsonPropertyName("buyerAddress")]
        public string? BuyerAddress { get; set; }

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("totalAmount")]
        public decimal? TotalAmount { get; set; }

        [JsonPropertyName("totalAmountInWords")]
        public string? TotalAmountInWords { get; set; }

        [JsonPropertyName("lookupUrl")]
        public string? LookupUrl { get; set; }

        [JsonPropertyName("lookupCode")]
        public string? LookupCode { get; set; }

        [JsonPropertyName("items")]
        public List<InvoiceLineItemDto> Items { get; set; } = new();

        [JsonPropertyName("validationStatus")]
        public DocumentValidationStatus? ValidationStatus { get; set; }

        [JsonPropertyName("validationErrors")]
        public List<string>? ValidationErrors { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "UPLOADED";

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }
    }

    public class InvoiceLineItemDto
    {
        [JsonPropertyName("itemOrder")]
        public int ItemOrder { get; set; } = 1;

        [JsonPropertyName("itemName")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; } = 1;

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; } = 0;

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; } = 0;
    }

    public class DocumentValidationStatus
    {
        [JsonPropertyName("isYearValid")]
        public bool? IsYearValid { get; set; }

        [JsonPropertyName("isDocTypeValid")]
        public bool? IsDocTypeValid { get; set; }

        [JsonPropertyName("isIdentityValid")]
        public bool? IsIdentityValid { get; set; }
    }
}
