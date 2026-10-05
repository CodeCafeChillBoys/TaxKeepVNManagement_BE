namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Gợi ý xử lý khi không có kết quả phù hợp (Đặc tả 2 - Mục 6 Luồng ngoại lệ)
    /// </summary>
    public class ExpertSearchSuggestionDto
    {
        public string Message { get; set; } = string.Empty;
        public bool SuggestRelaxRating { get; set; }
        public bool SuggestExpandPriceRange { get; set; }
        public bool SuggestExpandTimeFilter { get; set; }
        public string AiAssistantCtaUrl { get; set; } = "/assistant/chat";
        public string RequestSupportCtaUrl { get; set; } = "/consultation/request-match";
    }
}
