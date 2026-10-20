using System.Security.Cryptography;

namespace SmartPool.Application.Common.Helpers;

public static class TicketCodeGenerator
{
    /// <summary>Trả về chuỗi 6 chữ số ngẫu nhiên</summary>
    public static string Generate()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
