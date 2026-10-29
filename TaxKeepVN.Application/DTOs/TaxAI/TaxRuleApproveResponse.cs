using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class TaxRuleApproveResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("ruleSetId")]
        public Guid RuleSetId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("approvedBy")]
        public Guid? ApprovedBy { get; set; }

        [JsonPropertyName("approvedAt")]
        public DateTime? ApprovedAt { get; set; }
    }
}
