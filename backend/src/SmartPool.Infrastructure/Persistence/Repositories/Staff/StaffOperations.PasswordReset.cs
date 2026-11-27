using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    public async Task<AddEmployeeResponse> ResetStaffPasswordAsync(Guid userId, Guid actorId,
        string personalEmail, CancellationToken cancellationToken)
    {
        await RequireOnboardingAdminAsync(actorId, cancellationToken);
        // Lock the user before refresh-token rows, as in refresh rotation and password change.
        string temporaryPassword;
        string companyEmail;
        string fullName;
        DateTime expires;
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM smart_pool.users WHERE id = {userId} FOR UPDATE", cancellationToken);
            var employee = await context.Employees.Include(item => item.User).ThenInclude(item => item.Role)
                .Include(item => item.User.UserProfile).SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
                ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
            var user = employee.User;
            await context.Entry(user).ReloadAsync(cancellationToken);
            if (user.IsDeleted == true || user.Role?.Name.ToUpperInvariant() is not ("SALES" or "LIFEGUARD" or "COACH"))
                throw new StaffOnboardingException("EMPLOYEE_REQUIRED", 403);
            if (personalEmail.EndsWith("@smartpool.com", StringComparison.OrdinalIgnoreCase))
                throw PersonalEmailConflict();
            if (await context.Users.AsNoTracking().AnyAsync(item => item.Id != userId && item.Email != null &&
                item.Email.Trim().ToLower() == personalEmail, cancellationToken)) throw PersonalEmailConflict();
            temporaryPassword = TempPasswords.Generate();
            var now = OnboardingNow;
            expires = now.AddHours(24);
            user.Email = personalEmail;
            user.PasswordHash = Onboarding.Passwords.HashPassword(user, temporaryPassword);
            user.MustChangePassword = true;
            user.TemporaryPasswordExpiresAtUtc = expires;
            user.UpdatedAt = now;
            // Self-service recovery grants already bind to the old password fingerprint.
            await context.RefreshTokens.Where(item => item.UserId == userId && item.RevokeAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokeAt, now), cancellationToken);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: "23505", ConstraintName: "users_email_key" or "users_email_lower_key" or "users_normalized_email_onboarding_key" }) { throw PersonalEmailConflict(); }
            await transaction.CommitAsync(cancellationToken);
            companyEmail = employee.CompanyEmail ?? user.Username ?? user.Phone ?? personalEmail;
            fullName = user.UserProfile?.FullName ?? "Nhân viên";
        }
        var delivery = await SendEmployeeCredentialsAsync(fullName, personalEmail, companyEmail,
            temporaryPassword, expires, "Đặt lại mật khẩu SmartPool", cancellationToken);
        return new AddEmployeeResponse(userId, companyEmail, delivery)
        {
            Message = delivery == "FAILED"
                ? "Mật khẩu cũ không còn hiệu lực. Chưa gửi được email. Admin phải đặt lại mật khẩu để gửi lại."
                : null
        };
    }
}
