using System;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Service quản lý bộ nhớ đệm phân tán Redis cho toàn bộ hệ thống
    /// </summary>
    public interface IRedisCacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        Task RemoveAsync(string key);
        Task RemoveByPrefixAsync(string prefix);
    }
}
