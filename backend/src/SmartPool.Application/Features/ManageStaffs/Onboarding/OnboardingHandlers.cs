using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Onboarding;

public sealed class OnboardingHandlers(IStaffOperations operations) :
    IRequestHandler<OnboardingCommand, OnboardingWorkspace>,
    IRequestHandler<ListStaffInvitationsQuery, IReadOnlyList<StaffInvitationListItem>>,
    IRequestHandler<ReadStaffDocumentQuery, StaffDocumentContent>
{
    public Task<OnboardingWorkspace> Handle(OnboardingCommand request, CancellationToken cancellationToken) =>
        operations.ExecuteOnboardingAsync(request, cancellationToken);
    public Task<IReadOnlyList<StaffInvitationListItem>> Handle(ListStaffInvitationsQuery request, CancellationToken cancellationToken) =>
        operations.ListInvitationsAsync(request, cancellationToken);
    public Task<StaffDocumentContent> Handle(ReadStaffDocumentQuery request, CancellationToken cancellationToken) =>
        operations.ReadStaffDocumentAsync(request, cancellationToken);
}
