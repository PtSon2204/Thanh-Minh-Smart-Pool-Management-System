using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Services;

public sealed class StaffContractAccessPolicy(SmartPoolDbContext context, TimeProvider timeProvider)
    : IStaffContractAccessPolicy
{
    private static readonly JsonSerializerOptions ContractJson = new(JsonSerializerDefaults.Web);

    public async Task<StaffContractAccessDecision> EvaluateAsync(Guid userId, string primaryRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsStaffRole(primaryRole)) return new(StaffContractState.Exempt, true);
        var employee = await context.Employees.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.Status, item.ContractInactiveSinceUtc, item.ManualAccessEnabled }).SingleOrDefaultAsync(cancellationToken);
        if (employee is null) return new(StaffContractState.Exempt, true);
        if (employee.Status == "Working" && employee.ManualAccessEnabled)
            return new(StaffContractState.Exempt, true);

        var today = VietnamTimeBoundary.GetDate(timeProvider);
        var patterns = context.StaffScheduleChanges.AsNoTracking().Where(item => item.EmployeeId == userId &&
            item.Kind == "Pattern" && item.AppliedVersion != null);
        var revision = await patterns.Where(item => item.EffectiveFrom <= today)
            .OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.AppliedVersion)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await patterns.OrderBy(item => item.EffectiveFrom).ThenByDescending(item => item.AppliedVersion)
                .FirstOrDefaultAsync(cancellationToken);
        if (revision is null) return new(StaffContractState.Legacy, employee.Status == "Working");

        ContractTerms? contract;
        try { contract = JsonSerializer.Deserialize<ContractTerms>(revision.ContractJson!, ContractJson); }
        catch (JsonException) { return new(StaffContractState.Invalid, false, RevisionId: revision.Id); }
        if (contract is null || contract.StartDate == default || contract.EndDate < contract.StartDate ||
            revision.EffectiveFrom is null)
            return new(StaffContractState.Invalid, false, RevisionId: revision.Id);

        var state = today < contract.StartDate || today < revision.EffectiveFrom
            ? StaffContractState.NotStarted : today > contract.EndDate
                ? StaffContractState.Expired : StaffContractState.Active;
        var employmentEligible = employee.Status == "Working" ||
            employee.Status == "Inactive" && employee.ContractInactiveSinceUtc.HasValue;
        return new(state, state == StaffContractState.Active && employmentEligible,
            contract, revision.Id, revision.AppliedVersion, revision.EffectiveFrom);
    }

    private static bool IsStaffRole(string role) =>
        role.Equals("SALES", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("LIFEGUARD", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("COACH", StringComparison.OrdinalIgnoreCase);
}
