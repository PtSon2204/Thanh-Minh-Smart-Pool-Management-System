using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IUserOperations
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByPhoneOrUsernameAsync(string phoneOrUsername, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    Task<UserProfile?> GetUserProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<User>> GetUsersByRoleAsync(string roleName, CancellationToken cancellationToken);
    Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<List<User>> GetUsersByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);

    // Atomic compare-and-swap: the password, email and account eligibility must still match.
    Task<bool> TryUpdatePasswordAsync(Guid userId, string expectedEmail,
        string expectedPasswordHash, string newPasswordHash, CancellationToken cancellationToken);

    Task<bool> TryChangePasswordAsync(Guid userId, string expectedPasswordHash,
        string newPasswordHash, CancellationToken cancellationToken);

    Task<PagedResponse<GetUsersResponse>> GetUsersAsync(GetUsersQuery query, CancellationToken cancellationToken);
    Task<GetUserByIdResponse?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UpdateUserResponse?> UpdateUserAsync(UpdateUserValues values, CancellationToken cancellationToken);
    Task<ChangeUserRoleResponse?> ChangeUserRoleAsync(Guid userId, Guid actorId, SmartPool.Domain.Enums.RoleEnum role,
        CancellationToken cancellationToken);
    Task<ChangeUserStatusResponse?> ChangeUserStatusAsync(Guid userId, Guid actorId,
        SmartPool.Domain.Enums.UserStatusEnum status, CancellationToken cancellationToken);

    // Existence checks
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
    Task<bool> ExistsByPhoneAsync(string phone, string internationalPhone, CancellationToken cancellationToken);

    // Roles
    Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken);
    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken);

    // User & Profile Creation
    Task CreateUserWithProfileAsync(User user, UserProfile profile, Notification? notification, CancellationToken cancellationToken);

    // Lookups
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<List<User>> FindUsersByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<User?> FindActiveUserByEmailWithPasswordAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetActiveUserWithPasswordAsync(Guid userId, string email, CancellationToken cancellationToken);

    // Profiles
    Task AddUserProfileAsync(UserProfile profile, CancellationToken cancellationToken);
    Task UpdateUserProfileAsync(UserProfile profile, CancellationToken cancellationToken);
}
