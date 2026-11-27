using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class CompanyEmailGenerator(SmartPoolDbContext context) : ICompanyEmailGenerator
{
    private const string Domain = "smartpool.com";
    private const int CompanyEmailMaxLength = 100;
    private const int SuffixDigits = 3;
    private const int SuffixCount = 1000;

    public async Task<string> GenerateAsync(string fullName, IReadOnlySet<string> attempted, CancellationToken cancellationToken)
    {
        var localBase = BuildLocalBase(fullName);
        var taken = (await context.Employees.AsNoTracking()
                .Where(item => item.CompanyEmail != null && EF.Functions.Like(item.CompanyEmail, localBase + "%"))
                .Select(item => item.CompanyEmail!).ToListAsync(cancellationToken))
            .Concat(await context.Users.AsNoTracking()
                .Where(item => item.Email != null && EF.Functions.ILike(item.Email.Trim(), localBase + "%"))
                .Select(item => item.Email!).ToListAsync(cancellationToken))
            .Select(email => email.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var suffix in ShuffledSuffixes())
        {
            var email = $"{localBase}{suffix}@{Domain}";
            if (!taken.Contains(email) && !attempted.Contains(email)) return email;
        }
        throw new StaffOnboardingException("COMPANY_EMAIL_UNAVAILABLE", 409);
    }

    // Format: given name in full + first letter of every preceding token in
    // original order, lowercase, no diacritics (plan §3.3).
    private static string BuildLocalBase(string fullName)
    {
        var tokens = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken).ToArray();
        if (tokens.Length == 0 || tokens.Any(string.IsNullOrEmpty))
            throw new StaffOnboardingException("INVALID_FULL_NAME", 400);
        var given = tokens[^1];
        var initials = string.Concat(tokens.Take(tokens.Length - 1).Select(token => token[..1]));
        var local = given + initials;
        var maxLocal = CompanyEmailMaxLength - SuffixDigits - Domain.Length - 1;
        return local.Length > maxLocal ? local[..maxLocal] : local;
    }

    private static string NormalizeToken(string token)
    {
        var mapped = token.Replace('đ', 'd').Replace('Đ', 'D');
        var builder = new StringBuilder(mapped.Length);
        foreach (var character in mapped.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        var ascii = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        // Plan §3.3: the prefix uses lowercase ASCII letters only; name separators are
        // dropped and unusable names are rejected instead of producing an empty prefix.
        if (ascii.Any(character => !char.IsAsciiLetter(character) && character is not ('-' or '\'')))
            throw new StaffOnboardingException("INVALID_FULL_NAME", 400);
        return new string(ascii.Where(char.IsAsciiLetter).ToArray());
    }

    private static IEnumerable<string> ShuffledSuffixes()
    {
        var values = Enumerable.Range(0, SuffixCount).ToArray();
        RandomNumberGenerator.Shuffle(values.AsSpan());
        return values.Select(value => value.ToString($"D{SuffixDigits}"));
    }
}
