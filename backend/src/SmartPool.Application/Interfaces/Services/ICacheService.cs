namespace SmartPool.Application.Interfaces.Services
{
    public interface ICacheService
    {
        /// <summary>Lấy giá trị từ cache theo key. Trả về null nếu không có.</summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>Lưu giá trị vào cache với thời gian hết hạn tùy chọn.</summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

        /// <summary>Xóa key khỏi cache.</summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Kiểm tra key có tồn tại trong cache không.</summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    }
}
