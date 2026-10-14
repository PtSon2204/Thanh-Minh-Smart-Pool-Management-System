using System.Security.Cryptography;

namespace SmartPool.Application.Common.Helpers;

/// <summary>
/// Sinh mã vé ngắn 6 chữ số (000000–999999) dễ đọc và nhập tay.
/// </summary>
public static class TicketCodeGenerator
{
    /// <summary>Trả về chuỗi 6 chữ số ngẫu nhiên, ví dụ: "482731".</summary>
    public static string Generate()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
