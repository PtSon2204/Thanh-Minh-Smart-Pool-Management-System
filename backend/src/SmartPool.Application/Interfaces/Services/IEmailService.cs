namespace SmartPool.Application.Interfaces.Services
{
    /// <summary>
    /// Gửi email thông báo (kết quả kiểm tra nước, cảnh báo, v.v.).
    /// Implement ở Infrastructure/Services.
    /// </summary>
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
        Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default);
    }
}
