namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Kết quả trả về sau khi tải lên ảnh đại diện chuyên gia thành công
    /// </summary>
    public class UploadAvatarResponseDto
    {
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
