using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Domain.Enums;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class UserOperations : IUserOperations
{
    private readonly SmartPoolDbContext _context;

    public UserOperations(SmartPoolDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<GetUsersResponse>> GetUsersAsync(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = _context.Users.AsNoTracking().Where(user => user.IsDeleted != true);

        var searchTerm = request.SearchTerm?.Trim();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            var pattern = $"%{EscapeLike(searchTerm)}%";
            users = users.Where(user =>
                (user.Username != null && EF.Functions.ILike(user.Username, pattern, "\\")) ||
                (user.Email != null && EF.Functions.ILike(user.Email, pattern, "\\")) ||
                (user.Phone != null && EF.Functions.ILike(user.Phone, pattern, "\\")) ||
                (user.UserProfile != null && EF.Functions.ILike(user.UserProfile.FullName, pattern, "\\")));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim();
            users = users.Where(user => user.Role != null && EF.Functions.ILike(user.Role.Name, role));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            users = users.Where(user => user.Status != null && EF.Functions.ILike(user.Status, status));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var offset = ((long)request.PageIndex - 1) * request.PageSize;
        var items = offset > int.MaxValue
            ? []
            : await users.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
                .Skip((int)offset).Take(request.PageSize)
                .Select(user => new GetUsersResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Phone = user.Phone,
                    FullName = user.UserProfile != null ? user.UserProfile.FullName : null,
                    AvatarUrl = user.UserProfile != null ? user.UserProfile.AvatarUrl : null,
                    RoleId = user.RoleId,
                    Role = user.Role != null ? user.Role.Name : null,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt
                }).ToListAsync(cancellationToken);

        return new PagedResponse<GetUsersResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public Task<GetUserByIdResponse?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Users.AsNoTracking()
            .Where(user => user.Id == id && user.IsDeleted != true)
            .Select(user => new GetUserByIdResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Phone = user.Phone,
                FullName = user.UserProfile != null ? user.UserProfile.FullName : null,
                AvatarUrl = user.UserProfile != null ? user.UserProfile.AvatarUrl : null,
                Address = user.UserProfile != null ? user.UserProfile.Address : null,
                DateOfBirth = user.UserProfile != null ? user.UserProfile.DateOfBirth : null,
                RoleId = user.RoleId,
                Role = user.Role != null ? user.Role.Name : null,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            }).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UpdateUserResponse?> UpdateUserAsync(UpdateUserValues values, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(item => item.UserProfile)
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == values.Id && item.IsDeleted != true, cancellationToken);
        if (user is null)
            return null;

        if (await _context.Users.AnyAsync(item => item.Id != values.Id && item.Email != null &&
            item.Email.ToLower() == values.Email, cancellationToken))
            throw new UniqueUserFieldException("email");

        var internationalPhone = "+84" + values.Phone[1..];
        if (await _context.Users.AnyAsync(item => item.Id != values.Id &&
            (item.Phone == values.Phone || item.Phone == internationalPhone), cancellationToken))
            throw new UniqueUserFieldException("phone");

        var now = DateTime.UtcNow;
        user.Email = values.Email;
        user.Phone = values.Phone;
        user.UpdatedAt = now;

        var profile = user.UserProfile;
        if (profile is null)
        {
            profile = new SmartPool.Domain.Entities.UserProfile { UserId = user.Id, User = user };
            _context.UserProfiles.Add(profile);
            user.UserProfile = profile;
        }
        profile.FullName = values.FullName;
        profile.DateOfBirth = values.DateOfBirth;
        profile.Address = values.Address;
        profile.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "users_email_key" })
        {
            throw new UniqueUserFieldException("email", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "users_phone_key" })
        {
            throw new UniqueUserFieldException("phone", exception);
        }

        return new UpdateUserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Address = profile.Address,
            AvatarUrl = profile.AvatarUrl,
            Role = user.Role?.Name,
            Status = user.Status,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<ChangeUserRoleResponse?> ChangeUserRoleAsync(
        Guid userId, Guid actorId, RoleEnum requestedRole, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        // Serialize role changes so two admins cannot both demote the last active admins.
        await _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);

        var actor = await _context.Users.AsNoTracking()
            .Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == actorId && item.IsDeleted != true, cancellationToken);
        if (actor is null || !string.Equals(actor.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            actor.Role?.IsDeleted == true ||
            !string.Equals(actor.Role?.Name, nameof(RoleEnum.ADMIN), StringComparison.OrdinalIgnoreCase))
            throw new RoleChangeForbiddenException();

        var user = await _context.Users.Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == userId && item.IsDeleted != true, cancellationToken);
        if (user is null)
            return null;

        var roleName = requestedRole.ToString();
        var role = await _context.Roles.FirstOrDefaultAsync(item => item.Name.ToUpper() == roleName &&
            item.IsDeleted != true, cancellationToken);
        if (role is null)
            throw new RoleChangeConflictException("Vai trò chưa được cấu hình hoặc đã ngừng sử dụng.");

        if (actorId == userId && requestedRole != RoleEnum.ADMIN)
            throw new RoleChangeForbiddenException();

        if (user.RoleId != role.Id && IsActiveAdmin(user) && requestedRole != RoleEnum.ADMIN &&
            await IsLastActiveAdminAsync(cancellationToken))
            throw new RoleChangeConflictException("Hệ thống phải còn ít nhất một Admin đang hoạt động.");

        if (requestedRole == RoleEnum.CUSTOMER &&
            await _context.Employees.AnyAsync(item => item.UserId == userId, cancellationToken))
            throw new RoleChangeConflictException("Tài khoản có hồ sơ nhân viên phải hoàn tất quy trình ngừng làm trước khi chuyển sang CUSTOMER.");

        if (user.RoleId != role.Id)
        {
            user.RoleId = role.Id;
            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ChangeUserRoleResponse
        {
            Id = user.Id,
            RoleId = role.Id,
            Role = role.Name,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<ChangeUserStatusResponse?> ChangeUserStatusAsync(
        Guid userId, Guid actorId, UserStatusEnum requestedStatus, CancellationToken cancellationToken)
    {
        if (requestedStatus is not (UserStatusEnum.ACTIVE or UserStatusEnum.LOCKED))
            throw new StatusChangeConflictException("Trạng thái phải là ACTIVE hoặc LOCKED.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774201001)", cancellationToken);
        // Assignment uses the same employee lock, so it cannot race past a status change.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0))", cancellationToken);

        var actor = await _context.Users.AsNoTracking().Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == actorId && item.IsDeleted != true, cancellationToken);
        if (actor is null || !IsActiveAdmin(actor))
            throw new StatusChangeForbiddenException();

        var user = await _context.Users.Include(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == userId && item.IsDeleted != true, cancellationToken);
        if (user is null)
            return null;

        if (actorId == userId && requestedStatus == UserStatusEnum.LOCKED)
            throw new StatusChangeForbiddenException();

        var currentStatus = user.Status?.ToUpperInvariant();
        if (currentStatus is not (nameof(UserStatusEnum.ACTIVE) or nameof(UserStatusEnum.LOCKED)))
            throw new StatusChangeConflictException("Trạng thái hiện tại của tài khoản không hỗ trợ thao tác này.");

        if (requestedStatus == UserStatusEnum.LOCKED && currentStatus == nameof(UserStatusEnum.ACTIVE) &&
            IsActiveAdmin(user) && await IsLastActiveAdminAsync(cancellationToken))
            throw new StatusChangeConflictException("Hệ thống phải còn ít nhất một Admin đang hoạt động.");

        if (currentStatus != requestedStatus.ToString())
        {
            user.Status = requestedStatus.ToString();
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ChangeUserStatusResponse
        {
            Id = user.Id,
            Status = requestedStatus.ToString(),
            UpdatedAt = user.UpdatedAt
        };
    }

    private static bool IsActiveAdmin(SmartPool.Domain.Entities.User user) =>
        user.IsDeleted != true && user.Role?.IsDeleted != true &&
        string.Equals(user.Status, nameof(UserStatusEnum.ACTIVE), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(user.Role?.Name, nameof(RoleEnum.ADMIN), StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsLastActiveAdminAsync(CancellationToken cancellationToken) =>
        await _context.Users.CountAsync(item => item.IsDeleted != true && item.Role != null &&
            item.Role.IsDeleted != true && EF.Functions.ILike(item.Role.Name, nameof(RoleEnum.ADMIN)) &&
            item.Status != null && EF.Functions.ILike(item.Status, nameof(UserStatusEnum.ACTIVE)),
            cancellationToken) <= 1;

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
