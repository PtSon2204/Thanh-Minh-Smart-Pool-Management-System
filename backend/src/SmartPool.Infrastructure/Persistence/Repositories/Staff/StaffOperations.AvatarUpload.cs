using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff;

public sealed partial class StaffOperations
{
    // Plan §3.7: admin avatar upload for a staff account. Verifies the target is an
    // employee, then updates only that employee's UserProfile.AvatarUrl.
    public async Task<string> SetStaffAvatarAsync(Guid userId, Stream content, string fileName,
        string contentType, CancellationToken cancellationToken)
    {
        var user = await context.Users.Include(item => item.UserProfile)
            .SingleOrDefaultAsync(item => item.Id == userId && item.Employee != null, cancellationToken)
            ?? throw new StaffOnboardingException("EMPLOYEE_NOT_FOUND", 404);
        var avatarUrl = await Storage.UploadAsync(content, fileName, contentType, cancellationToken);
        var profile = user.UserProfile;
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId, FullName = user.Username ?? "Nhân viên",
                AvatarUrl = avatarUrl, UpdatedAt = OnboardingNow
            };
            context.UserProfiles.Add(profile);
        }
        else
        {
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = OnboardingNow;
        }
        await context.SaveChangesAsync(cancellationToken);
        return avatarUrl;
    }
}
