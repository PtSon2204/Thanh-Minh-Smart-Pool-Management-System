using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.Application.Interfaces.Services;

public enum StaffContractState { Exempt, Legacy, Active, NotStarted, Expired, Invalid }

public sealed record StaffContractAccessDecision(StaffContractState State, bool IsAllowed,
    ContractTerms? Contract = null, Guid? RevisionId = null, long? AppliedVersion = null,
    DateOnly? EffectiveFrom = null)
{
    public bool HasContract => State is not (StaffContractState.Exempt or StaffContractState.Legacy);
}

public interface IStaffContractAccessPolicy
{
    Task<StaffContractAccessDecision> EvaluateAsync(Guid userId, string primaryRole,
        CancellationToken cancellationToken = default);
}
