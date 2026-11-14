using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    public async Task<GetClassRegistrantContactResponse> GetClassRegistrantContactAsync(
        Guid classId, Guid customerUserId, Guid coachUserId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await context.SwimClasses.AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => (Guid?)item.CreatedByUserId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp dạy bơi.");
        if (ownerUserId != coachUserId)
            throw new UnauthorizedAccessException("Chỉ Coach tạo lớp được xem thông tin người đăng ký.");

        return await context.ClassRegistrations.AsNoTracking()
            .Where(item => item.ClassId == classId && item.CustomerUserId == customerUserId)
            .Select(item => new GetClassRegistrantContactResponse(
                item.CustomerUserId,
                item.CustomerUser.UserProfile != null
                    ? item.CustomerUser.UserProfile.FullName
                    : item.CustomerUser.Username ?? string.Empty,
                item.CustomerUser.Email,
                item.CustomerUser.Phone))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy người đăng ký trong lớp.");
    }
}
