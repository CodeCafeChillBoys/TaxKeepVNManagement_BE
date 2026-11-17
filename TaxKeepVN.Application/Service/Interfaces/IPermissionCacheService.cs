namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IPermissionCacheService
    {
        /// <summary>
        /// Kiểm tra vai trò (theo ID) có sở hữu mã quyền tương ứng hay không (tra cứu qua Redis Cache)
        /// </summary>
        Task<bool> HasPermissionAsync(int roleId, string permissionCode);

        /// <summary>
        /// Kiểm tra vai trò (theo mã vai trò: taxpayer, admin, expert) có sở hữu mã quyền hay không
        /// </summary>
        Task<bool> HasPermissionAsync(string roleCode, string permissionCode);

        /// <summary>
        /// Xóa cache quyền của vai trò khi Admin cập nhật phân quyền
        /// </summary>
        Task InvalidateRolePermissionsAsync(int roleId);

        /// <summary>
        /// Nạp trước toàn bộ quyền của vai trò vào Redis Cache
        /// </summary>
        Task WarmupRolePermissionsAsync(int roleId);
    }
}
