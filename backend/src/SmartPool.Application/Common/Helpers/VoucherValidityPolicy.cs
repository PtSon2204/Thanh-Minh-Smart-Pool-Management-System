using System.Linq.Expressions;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Common.Helpers;

public static class VoucherValidityPolicy
{
    public const string Active = "ACTIVE";
    public const string Expired = "EXPIRED";
    public const string Scheduled = "SCHEDULED";
    public const string Locked = "LOCKED";

    // IsActive is the manual lock; validity is the UTC interval [StartDate, EndDate).
    public static string GetStatus(Voucher voucher, DateTime nowUtc)
    {
        if (voucher.EndDate <= nowUtc) return Expired;
        if (voucher.IsActive != true) return Locked;
        if (voucher.StartDate > nowUtc) return Scheduled;
        return Active;
    }

    public static Expression<Func<Voucher, bool>> UsableCodeAt(string code, DateTime nowUtc)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return voucher => voucher.Code == normalizedCode
            && voucher.IsDeleted != true
            && voucher.IsActive == true
            && (voucher.StartDate == null || voucher.StartDate <= nowUtc)
            && (voucher.EndDate == null || voucher.EndDate > nowUtc);
    }

    public static Expression<Func<Voucher, bool>> WithStatus(string status, DateTime nowUtc) => status switch
    {
        Active => voucher => voucher.IsActive == true
            && (voucher.StartDate == null || voucher.StartDate <= nowUtc)
            && (voucher.EndDate == null || voucher.EndDate > nowUtc),
        Expired => voucher => voucher.EndDate != null && voucher.EndDate <= nowUtc,
        Scheduled => voucher => voucher.IsActive == true && voucher.StartDate > nowUtc
            && (voucher.EndDate == null || voucher.EndDate > nowUtc),
        Locked => voucher => voucher.IsActive != true
            && (voucher.EndDate == null || voucher.EndDate > nowUtc),
        _ => throw new ArgumentException("Trạng thái voucher không hợp lệ.", nameof(status))
    };
}
