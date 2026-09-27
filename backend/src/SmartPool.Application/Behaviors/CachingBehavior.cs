using MediatR;
using Microsoft.Extensions.Logging;
using SmartPool.Application.Interfaces.Queries;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Behaviors
{
    /// <summary>
    /// Pipeline behavior: cache kết quả của Query nếu Query implement ICacheableQuery.
    /// Command thì bỏ qua, chỉ áp dụng cho Query có đánh dấu cacheable.
    /// </summary>
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ICacheService _cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
        {
            _cache  = cache;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Chỉ cache nếu Request implement ICacheableQuery
            if (request is not ICacheableQuery cacheableQuery)
                return await next();

            var cached = await _cache.GetAsync<TResponse>(cacheableQuery.CacheKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogDebug("[CACHE HIT] {Key}", cacheableQuery.CacheKey);
                return cached;
            }

            var response = await next();
            await _cache.SetAsync(cacheableQuery.CacheKey, response, cacheableQuery.CacheExpiry, cancellationToken);
            _logger.LogDebug("[CACHE SET] {Key}", cacheableQuery.CacheKey);

            return response;
        }
    }
}
