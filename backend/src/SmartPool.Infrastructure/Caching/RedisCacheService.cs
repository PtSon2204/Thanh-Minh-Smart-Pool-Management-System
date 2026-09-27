using Microsoft.Extensions.Caching.Distributed;
using SmartPool.Application.Interfaces.Services;
using System.Text.Json;

namespace SmartPool.Infrastructure.Caching
{
    /// <summary>
    /// Distributed cache wrapper dùng Redis (IDistributedCache).
    /// Serialize/deserialize bằng System.Text.Json.
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            return data is null ? default : JsonSerializer.Deserialize<T>(data);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            var options = new DistributedCacheEntryOptions();
            if (expiry.HasValue)
                options.SetAbsoluteExpiration(expiry.Value);

            var data = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, data, options, cancellationToken);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => await _cache.RemoveAsync(key, cancellationToken);

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => await _cache.GetStringAsync(key, cancellationToken) is not null;
    }
}
