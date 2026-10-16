using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IUserOperations
{
    // Atomic compare-and-swap: the password, email and account eligibility must still match.
    Task<bool> TryUpdatePasswordAsync(Guid userId, string expectedEmail,
        string expectedPasswordHash, string newPasswordHash, CancellationToken cancellationToken);

    Task<PagedResponse<GetUsersResponse>> GetUsersAsync(GetUsersQuery query, CancellationToken cancellationToken);
    Task<GetUserByIdResponse?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UpdateUserResponse?> UpdateUserAsync(UpdateUserValues values, CancellationToken cancellationToken);
    Task<ChangeUserRoleResponse?> ChangeUserRoleAsync(Guid userId, Guid actorId, SmartPool.Domain.Enums.RoleEnum role,
        CancellationToken cancellationToken);
    Task<ChangeUserStatusResponse?> ChangeUserStatusAsync(Guid userId, Guid actorId,
        SmartPool.Domain.Enums.UserStatusEnum status, CancellationToken cancellationToken);
}
