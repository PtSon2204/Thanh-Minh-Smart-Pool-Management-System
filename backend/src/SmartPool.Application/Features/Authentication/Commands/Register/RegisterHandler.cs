using MediatR;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using FluentValidation;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResponse>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserProfile> _profiles;
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    private readonly IValidator<RegisterCommand > _validator;

    public RegisterHandler(
        IRepository<User> users,
        IRepository<UserProfile> profiles,
        IRepository<Role> roles,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<RegisterCommand> validator)
    {
        _users = users;
        _profiles = profiles;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var inputPhone = request.Phone.Trim();
        var phone = inputPhone.StartsWith("+84", StringComparison.Ordinal)
            ? "0" + inputPhone[3..]
            : inputPhone;
        var fullName = request.FullName.Trim();
        var address = request.Address?.Trim();
        if (address == string.Empty)
            address = null;

        // SQL unique indexes also cover soft-deleted users.
        if (await _users.ExistsAsync(
                user => user.Username != null && user.Username.ToLower() == username,
                cancellationToken))
            throw new RegisterConflictException(nameof(RegisterCommand.Username));

        if (await _users.ExistsAsync(
                user => user.Email != null && user.Email.ToLower() == email,
                cancellationToken))
            throw new RegisterConflictException(nameof(RegisterCommand.Email));

        var internationalPhone = phone.StartsWith('0') ? "+84" + phone[1..] : inputPhone;
        if (await _users.ExistsAsync(
                user => user.Phone == phone || user.Phone == internationalPhone,
                cancellationToken))
            throw new RegisterConflictException(nameof(RegisterCommand.Phone));

        var customerRole = await _roles.FirstOrDefaultAsync(
            role => role.Name == nameof(RoleEnum.CUSTOMER) && role.IsDeleted != true,
            cancellationToken);
        if (customerRole is null)
            throw new InvalidOperationException("Role CUSTOMER chưa tồn tại trong bảng roles.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            Phone = phone,
            RoleId = customerRole.Id,
            Status = "Active",
            IsDeleted = false
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var profile = new UserProfile
        {
            UserId = user.Id,
            User = user,
            FullName = fullName,
            DateOfBirth = request.DateOfBirth,
            Address = address
        };
        user.UserProfile = profile;

        await _users.AddAsync(user, cancellationToken);
        await _profiles.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterResponse
        {
            Id = user.Id,
            Username = username,
            Email = email,
            Phone = phone,
            FullName = fullName,
            Role = RoleEnum.CUSTOMER,
            Status = user.Status,
            CreatedAt = user.CreatedAt ?? DateTime.UtcNow
        };
    }


    public sealed class RegisterConflictException : Exception
    {
        public string Field { get; }

        public RegisterConflictException(string field)
            : base($"The field '{field}' already exists.")
        {
            Field = field;
        }
    }


    public sealed class CustomerRoleNotConfiguredException : Exception
    {
        public CustomerRoleNotConfiguredException()
            : base("Role CUSTOMER chưa tồn tại trong hệ thống.")
        {
        }
    }

}
