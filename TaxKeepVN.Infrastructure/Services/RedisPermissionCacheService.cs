using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVN.Infrastructure.Services
{
    /// <summary>
    /// Quản lý phân quyền qua Redis Cache.
    /// Luồng xử lý: Tra cứu Redis Set -> Nếu Cache Miss thì nạp từ PostgreSQL DB vào Redis -> Trả về kết quả.
    /// </summary>
    public class RedisPermissionCacheService : IPermissionCacheService
    {
        private readonly IConnectionMultiplexer? _redis;
        private readonly TaxKeepDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RedisPermissionCacheService> _logger;

        private const string CacheKeyPrefix = "taxkeep:role_permissions:";
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

        // Mapping mặc định mã vai trò sang Role ID để tránh query DB
        private static readonly Dictionary<string, int> RoleCodeToIdMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "taxpayer", 1 },
            { "admin", 2 },
            { "expert", 3 }
        };

        public RedisPermissionCacheService(
            TaxKeepDbContext dbContext,
            IConfiguration configuration,
            ILogger<RedisPermissionCacheService> logger,
            IConnectionMultiplexer? redis = null)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
            _redis = redis;
        }

        private bool IsRedisEnabled
        {
            get
            {
                var envEnabled = Environment.GetEnvironmentVariable("REDIS_ENABLED");
                if (!string.IsNullOrEmpty(envEnabled) && bool.TryParse(envEnabled, out var envBool))
                    return envBool;

                return _configuration.GetValue<bool>("Redis:Enabled", true);
            }
        }

        public async Task<bool> HasPermissionAsync(int roleId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            var normalizedCode = permissionCode.Trim().ToLowerInvariant();

            // 1. Kiểm tra qua Redis Cache
            if (IsRedisEnabled && _redis != null && _redis.IsConnected)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var key = $"{CacheKeyPrefix}{roleId}";

                    var keyExists = await db.KeyExistsAsync(key);
                    if (!keyExists)
                    {
                        // Cache Miss -> Nạp từ DB vào Redis
                        await PopulateRedisFromDbAsync(db, roleId);
                    }

                    return await db.SetContainsAsync(key, normalizedCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi truy cập Redis khi kiểm tra quyền RoleId={RoleId}, Code={Code}. Tự động truy vấn trực tiếp DB.", roleId, permissionCode);
                }
            }

            // 2. Nếu Redis chưa kết nối được hoặc có sự cố -> Truy vấn trực tiếp từ PostgreSQL Database
            return await _dbContext.RolePermissions
                .AsNoTracking()
                .AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Code.ToLower() == normalizedCode);
        }

        public async Task<bool> HasPermissionAsync(string roleCode, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
                return false;

            var normalizedRole = roleCode.Trim().ToLowerInvariant();
            if (RoleCodeToIdMap.TryGetValue(normalizedRole, out var roleId))
            {
                return await HasPermissionAsync(roleId, permissionCode);
            }

            // Tra cứu RoleId từ DB nếu vai trò không nằm trong map mặc định
            var role = await _dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code.ToLower() == normalizedRole);

            if (role == null)
                return false;

            return await HasPermissionAsync(role.Id, permissionCode);
        }

        public async Task InvalidateRolePermissionsAsync(int roleId)
        {
            if (IsRedisEnabled && _redis != null && _redis.IsConnected)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var key = $"{CacheKeyPrefix}{roleId}";
                    await db.KeyDeleteAsync(key);
                    _logger.LogInformation("Đã xóa key Redis cache phân quyền cho RoleId={RoleId}", roleId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi xóa Redis cache phân quyền RoleId={RoleId}", roleId);
                }
            }
        }

        public async Task WarmupRolePermissionsAsync(int roleId)
        {
            if (IsRedisEnabled && _redis != null && _redis.IsConnected)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    await PopulateRedisFromDbAsync(db, roleId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi nạp trước cache Redis cho RoleId={RoleId}", roleId);
                }
            }
        }

        private async Task PopulateRedisFromDbAsync(IDatabase db, int roleId)
        {
            var key = $"{CacheKeyPrefix}{roleId}";

            var permissions = await _dbContext.RolePermissions
                .AsNoTracking()
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission.Code.ToLower())
                .ToListAsync();

            var batch = new List<RedisValue>();
            if (permissions.Count > 0)
            {
                batch.AddRange(permissions.Select(p => (RedisValue)p));
            }
            else
            {
                // Thêm marker giả để đánh dấu đã load từ DB (tránh lặp lại DB query khi vai trò chưa có quyền nào)
                batch.Add((RedisValue)"__NONE__");
            }

            await db.KeyDeleteAsync(key);
            await db.SetAddAsync(key, batch.ToArray());
            await db.KeyExpireAsync(key, DefaultTtl);

            _logger.LogInformation("Đã nạp {Count} quyền từ DB vào Redis Set cho RoleId={RoleId}", permissions.Count, roleId);
        }
    }
}
