using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Constants;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.ManageStaffs;

// Focused check for the pre-contract credential-setup exception in
// StaffContractLifecycle: only NotStarted contracts defer the lifecycle lock.
public sealed class StaffContractLifecycleFlaggedAccountTests
{
    [Fact]
    public async Task ForcedPasswordSetupOnlyDefersLocksForNotStartedContracts()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var db = new SmartPoolDbContext(options);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var clock = new Clock(new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0, 0)), TimeSpan.Zero));
        var now = clock.UtcNow.UtcDateTime;
        var role = await db.Roles.FirstAsync(item => item.Name == "SALES" && item.IsDeleted != true);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var specs = new Dictionary<string, (DateOnly Start, DateOnly End, string Emp, DateTime? Since, string User, bool Flagged)>
        {
            ["flag_future_active"] = (today.AddDays(5), today.AddDays(35), "Working", null, "Active", true),
            ["flag_future_locked"] = (today.AddDays(5), today.AddDays(35), "Inactive", now, "LOCKED", true),
            ["flag_expired"] = (today.AddDays(-30), today.AddDays(-10), "Working", null, "Active", true),
            ["flag_admin_off"] = (today.AddDays(5), today.AddDays(35), "Inactive", null, "LOCKED", true),
            ["plain_future"] = (today.AddDays(5), today.AddDays(35), "Working", null, "Active", false),
        };
        var ids = new Dictionary<string, Guid>();
        try
        {
            foreach (var (key, spec) in specs)
            {
                var id = Guid.NewGuid();
                ids[key] = id;
                db.Users.Add(new User
                {
                    Id = id, RoleId = role.Id, Username = $"lc_{key}_{suffix}",
                    Email = $"{key}{suffix}@x.test", Status = spec.User, IsDeleted = false,
                    MustChangePassword = spec.Flagged
                });
                db.Employees.Add(new Employee
                {
                    UserId = id, Status = spec.Emp, JoinDate = spec.Start,
                    ContractInactiveSinceUtc = spec.Since
                });
                db.StaffScheduleChanges.Add(new StaffScheduleChange
                {
                    EmployeeId = id, Kind = "Pattern", AppliedVersion = 1, EffectiveFrom = spec.Start,
                    CreatedBy = id, CreatedAtUtc = now,
                    ContractJson = JsonSerializer.Serialize(
                        new ContractTerms("FULL_TIME", spec.Start, spec.End,
                            Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day,
                                day == 1 ? [FixedShifts.All[0].Id] : [])).ToArray()),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                });
            }
            await db.SaveChangesAsync();

            var lifecycle = new StaffContractLifecycle(db, new StaffContractAccessPolicy(db, clock), clock);
            foreach (var id in ids.Values)
                await lifecycle.SynchronizeEmployeeAsync(id);

            async Task<Employee> Employee(string key) =>
                await db.Employees.AsNoTracking().SingleAsync(item => item.UserId == ids[key]);
            async Task<string> UserStatus(string key) =>
                await db.Users.AsNoTracking().Where(item => item.Id == ids[key]).Select(item => item.Status!).SingleAsync();

            // Future contract: credential setup keeps the account usable.
            Assert.Equal("Working", (await Employee("flag_future_active")).Status);
            Assert.Equal("Active", await UserStatus("flag_future_active"));

            // Lifecycle-owned lock for a future contract is restored.
            var restored = await Employee("flag_future_locked");
            Assert.Equal("Working", restored.Status);
            Assert.Null(restored.ContractInactiveSinceUtc);
            Assert.Equal("ACTIVE", await UserStatus("flag_future_locked"));

            // Expired contract: the normal lock applies even while flagged.
            var expired = await Employee("flag_expired");
            Assert.Equal("Inactive", expired.Status);
            Assert.NotNull(expired.ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await UserStatus("flag_expired"));

            // Admin deactivation (no lifecycle timestamp) is never reversed.
            var adminOff = await Employee("flag_admin_off");
            Assert.Equal("Inactive", adminOff.Status);
            Assert.Null(adminOff.ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await UserStatus("flag_admin_off"));

            // Control: a non-flagged future contract is locked as before.
            var plain = await Employee("plain_future");
            Assert.Equal("Inactive", plain.Status);
            Assert.NotNull(plain.ContractInactiveSinceUtc);
            Assert.Equal("LOCKED", await UserStatus("plain_future"));
        }
        finally
        {
            var list = ids.Values.ToList();
            await db.StaffScheduleChanges.Where(item => list.Contains(item.EmployeeId)).ExecuteDeleteAsync();
            await db.Employees.Where(item => list.Contains(item.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(item => list.Contains(item.Id)).ExecuteDeleteAsync();
        }
    }

    private sealed class Clock(DateTimeOffset instant) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = instant;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
