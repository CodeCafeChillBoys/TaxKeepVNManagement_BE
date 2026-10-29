using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IJwtTokenService
    {
        /// <summary>Tạo JWT token từ thông tin User</summary>
        string GenerateToken(User user);
    }
}
