using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services;

public sealed partial class ServiceOperations
{
    private static string RentalRequestHash(object payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));

    private Task<int> LockRentalRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({requestId.ToString()}, 0))", cancellationToken);

    private async Task<T?> RentalReplayAsync<T>(Guid requestId, string hash, Guid actorId, CancellationToken cancellationToken,
        string? legacyCashHash = null) where T : class
    {
        var saved = await _context.RentalTransactions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (saved is null)
        {
            if (await _context.RentalPaymentIntents.AnyAsync(item => item.Id == requestId, cancellationToken))
                throw new ServiceConflictException("Mã yêu cầu đã thuộc một giao dịch QR. Hãy tiếp tục giao dịch đã tạo.");
            return null;
        }
        var legacyCashReplay = legacyCashHash is not null && saved.Kind == "Collection" && saved.RequestHash == legacyCashHash
            && await _context.Payments.AnyAsync(item => item.Id == saved.PaymentId && item.PaymentMethod == "MANUAL", cancellationToken);
        if ((saved.RequestHash != hash && !legacyCashReplay) || saved.ConfirmedByUserId != actorId)
            throw new ServiceConflictException("Mã yêu cầu đã được sử dụng với thông tin khác. Không gửi lại thành một giao dịch mới.");
        return JsonSerializer.Deserialize<T>(saved.ResultJson) ?? throw new InvalidOperationException("Saved rental outcome is invalid.");
    }
}
