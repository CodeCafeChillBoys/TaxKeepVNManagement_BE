namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Thông tin tóm tắt lĩnh vực chuyên môn trong hồ sơ
    /// </summary>
    public class SpecializationDtoItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
