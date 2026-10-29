namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Tiêu chí sắp xếp danh sách chuyên gia (Đặc tả 2 - Mục 5 & 8)
    /// </summary>
    public enum ExpertSortBy
    {
        /// <summary>Đề xuất ưu tiên (Mặc định BR-02: Rating, số ca hoàn thành, có lịch rảnh sớm)</summary>
        Recommended = 0,

        /// <summary>Điểm đánh giá sao giảm dần (Cao -> Thấp)</summary>
        RatingDesc = 1,

        /// <summary>Mức phí tư vấn tăng dần (Thấp -> Cao)</summary>
        FeeAsc = 2,

        /// <summary>Mức phí tư vấn giảm dần (Cao -> Thấp)</summary>
        FeeDesc = 3,

        /// <summary>Số năm kinh nghiệm nhiều nhất</summary>
        ExperienceDesc = 4,

        /// <summary>Số ca tư vấn đã hoàn thành nhiều nhất</summary>
        CompletedSessionsDesc = 5
    }
}
