namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;

public static class ClassRegistrationCancellationPolicy
{
    public static bool CanSelfCancel(DateTime? firstScheduledStart, DateTime nowUtc) =>
        firstScheduledStart is null || nowUtc < firstScheduledStart.Value;
}
