using System;
using TaxKeepVN.Application.DTOs.TaxSettlement;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDownloadTokenService
    {
        /// <summary>
        /// Sinh Signed Download Token có thời hạn sử dụng.
        /// </summary>
        /// <param name="payload">Dữ liệu hồ sơ cần tải</param>
        /// <param name="validity">Thời gian hiệu lực (mặc định 30 phút)</param>
        /// <returns>Chuỗi token an toàn dạng URL-safe</returns>
        string GenerateToken(DownloadTokenPayload payload, TimeSpan? validity = null);

        /// <summary>
        /// Xác thực và giải mã Signed Download Token.
        /// </summary>
        /// <param name="token">Chuỗi token nhận được từ URL</param>
        /// <returns>Payload nếu token hợp lệ và còn hạn; null nếu không hợp lệ hoặc đã hết hạn</returns>
        DownloadTokenPayload? ValidateToken(string token);
    }
}
