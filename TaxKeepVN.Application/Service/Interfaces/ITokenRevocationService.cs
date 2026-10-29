using System.Threading.Tasks;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITokenRevocationService
    {
        /// <summary>Thêm JTI vào danh sách token đã bị thu hồi</summary>
        Task RevokeAsync(string jti, System.DateTimeOffset expiresAt);

        /// <summary>Kiểm tra JTI có nằm trong danh sách bị thu hồi không</summary>
        Task<bool> IsRevokedAsync(string jti);
    }
}
