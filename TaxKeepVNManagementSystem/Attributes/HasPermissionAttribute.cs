using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Attributes
{
    /// <summary>
    /// Attribute kiểm tra quyền động từ Redis Cache/Database theo mã quyền (Permission Code).
    /// Ví dụ: [HasPermission("settlement.policy_manage")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class HasPermissionAttribute : TypeFilterAttribute
    {
        public string Permission { get; }

        public HasPermissionAttribute(string permission) : base(typeof(PermissionAuthorizationFilter))
        {
            Permission = permission;
            Arguments = new object[] { permission };
        }
    }

    /// <summary>
    /// Filter thực thi kiểm tra quyền của người dùng trước khi vào Action.
    /// Tra cứu tức thì từ Upstash Redis (Set Contains O(1)), tự động fallback DB nếu Redis tạm thời ngắt kết nối.
    /// </summary>
    public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permission;
        private readonly IPermissionCacheService _permissionCacheService;

        public PermissionAuthorizationFilter(string permission, IPermissionCacheService permissionCacheService)
        {
            _permission = permission;
            _permissionCacheService = permissionCacheService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // 1. Kiểm tra xác thực (Authentication)
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new ObjectResult(ApiResponse<object>.Fail("UNAUTHORIZED", "Vui lòng đăng nhập để tiếp tục."))
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // 2. Lấy định danh Role từ JWT Claims (roleId hoặc role name)
            var roleIdClaim = user.FindFirst("roleId")?.Value;
            bool hasPermission = false;

            if (int.TryParse(roleIdClaim, out var roleId))
            {
                hasPermission = await _permissionCacheService.HasPermissionAsync(roleId, _permission);
            }
            else
            {
                var roleCode = user.FindFirst(ClaimTypes.Role)?.Value 
                            ?? user.FindFirst("role")?.Value;

                if (!string.IsNullOrEmpty(roleCode))
                {
                    hasPermission = await _permissionCacheService.HasPermissionAsync(roleCode, _permission);
                }
            }

            // 3. Nếu không có quyền -> Chặn lại với mã lỗi 403 Forbidden chuẩn
            if (!hasPermission)
            {
                context.Result = new ObjectResult(ApiResponse<object>.Fail("FORBIDDEN", $"Bạn không có quyền thực hiện tính năng này (Yêu cầu quyền: {_permission})."))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
