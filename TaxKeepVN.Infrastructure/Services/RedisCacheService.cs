using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Services
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IConnectionMultiplexer? _redis;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RedisCacheService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public RedisCacheService(
            IConfiguration configuration,
            ILogger<RedisCacheService> logger,
            IConnectionMultiplexer? redis = null)
        {
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

        public async Task<T?> GetAsync<T>(string key)
        {
            if (!IsRedisEnabled || _redis == null || !_redis.IsConnected)
                return default;

            try
            {
                var db = _redis.GetDatabase();
                var value = await db.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                    return default;

                return JsonSerializer.Deserialize<T>(value!, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi đọc Redis key {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (!IsRedisEnabled || _redis == null || !_redis.IsConnected)
                return;

            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(value, JsonOptions);
                await db.StringSetAsync(key, json);
                if (expiration.HasValue)
                {
                    await db.KeyExpireAsync(key, expiration.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi ghi Redis key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!IsRedisEnabled || _redis == null || !_redis.IsConnected)
                return;

            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi xóa Redis key {Key}", key);
            }
        }

        public async Task RemoveByPrefixAsync(string prefix)
        {
            if (!IsRedisEnabled || _redis == null || !_redis.IsConnected)
                return;

            try
            {
                var server = _redis.GetServers()[0];
                var db = _redis.GetDatabase();
                var pattern = prefix.EndsWith("*") ? prefix : $"{prefix}*";
                foreach (var key in server.Keys(pattern: pattern))
                {
                    await db.KeyDeleteAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi xóa Redis keys theo prefix {Prefix}", prefix);
            }
        }
    }
}
