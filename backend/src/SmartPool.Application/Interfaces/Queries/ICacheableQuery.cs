namespace SmartPool.Application.Interfaces.Queries
{
    /// <summary>
    /// Đánh dấu Query muốn được cache tự động qua CachingBehavior.
    /// Implement interface này trong Query class của bạn.
    /// </summary>
    /// <example>
    /// public record GetPoolsQuery : IRequest&lt;List&lt;PoolDto&gt;&gt;, ICacheableQuery
    /// {
    ///     public string CacheKey =&gt; "pools:all";
    ///     public TimeSpan? CacheExpiry =&gt; TimeSpan.FromMinutes(5);
    /// }
    /// </example>
    public interface ICacheableQuery
    {
        /// <summary>Key duy nhất để lưu/lấy cache. Nên prefix theo feature: "pools:all", "tickets:123"</summary>
        string CacheKey { get; }

        /// <summary>Thời gian cache hết hạn. Null = không hết hạn cho đến khi bị xóa thủ công.</summary>
        TimeSpan? CacheExpiry { get; }
    }
}
