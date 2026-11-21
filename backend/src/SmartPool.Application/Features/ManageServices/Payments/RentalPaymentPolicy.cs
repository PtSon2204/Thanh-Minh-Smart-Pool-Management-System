using SmartPool.Application.Common.Helpers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartPool.Application.Features.ManageServices.Payments;

public static partial class RentalPaymentPolicy
{
    public const string Provider = "SePay";
    public const string Pending = "PENDING";
    public const string Partial = "PARTIAL";
    public const string Paid = "PAID";
    public const string Issued = "ISSUED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public static TimeSpan Lifetime => OnlineOrderPaymentPolicy.Lifetime;

    public static bool IsWaiting(string status) => status is Pending or Partial;
    public static bool HasExpired(string status, DateTime expiresAtUtc, DateTime nowUtc) =>
        IsWaiting(status) && nowUtc >= expiresAtUtc;
    public static decimal Remaining(decimal due, decimal received) => Math.Max(0, due - received);
    public static decimal Excess(decimal due, decimal received) => Math.Max(0, received - due);

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:RN(?:[0-9A-F]{32}|[0-9A-F]{6})|SP[0-9A-F]{6})(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    public static string? ExtractReference(string? code, string? content)
    {
        var references = ReferencePattern().Matches($"{code}\n{content}")
            .Select(match => match.Value.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (references.Length == 1) return references[0];

        // SePay can extract a six-character suffix from an existing long rental memo.
        var contentReferences = ReferencePattern().Matches(content ?? string.Empty);
        if (references.Length == 2 && contentReferences.Count == 1)
        {
            var legacyReference = contentReferences[0].Value.ToUpperInvariant();
            if (legacyReference.Length == 34 && IsRentalReference(legacyReference)
                && string.Equals(code?.Trim(), legacyReference[..8], StringComparison.OrdinalIgnoreCase))
                return legacyReference;
        }
        return null;
    }

    public static bool IsRentalReference(string reference) =>
        reference.Length is 8 or 34 && reference.StartsWith("RN", StringComparison.Ordinal)
        && reference[2..].All(Uri.IsHexDigit);

    public static bool IsTicketReference(string reference) => reference.Length == 8
        && reference.StartsWith("SP", StringComparison.Ordinal)
        && reference[2..].All(Uri.IsHexDigit);

    public static bool IsWholeVnd(decimal amount) => amount > 0 && decimal.Truncate(amount) == amount;

    public static string FinancialHash(BankTransferEvent transfer)
    {
        var identity = string.Join('\n', Provider, transfer.EventId.ToString(CultureInfo.InvariantCulture),
            transfer.AccountNumber.Trim(), transfer.Amount.ToString("0", CultureInfo.InvariantCulture),
            transfer.Reference.ToUpperInvariant(), transfer.BankReferenceCode?.Trim() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
