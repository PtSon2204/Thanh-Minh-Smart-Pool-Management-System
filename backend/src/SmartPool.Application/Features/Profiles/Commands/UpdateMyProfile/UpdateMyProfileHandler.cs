using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using System.Text.Json;

namespace SmartPool.Application.Features.Profiles.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileHandler : IRequestHandler<UpdateMyProfileCommand, IActionResult>
{
    private readonly IUserOperations _userOperations;
    private readonly IValidator<UpdateMyProfileCommand> _validator;

    public UpdateMyProfileHandler(
        IUserOperations userOperations,
        IValidator<UpdateMyProfileCommand> validator)
    {
        _userOperations = userOperations;
        _validator = validator;
    }

    public async Task<IActionResult> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
            
            var profile = await _userOperations.GetUserProfileByUserIdAsync(request.UserId, cancellationToken);
            if (profile == null) 
            {
                // Create profile if missing
                var user = await _userOperations.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) return new NotFoundObjectResult(new { message = "Tài khoản không tồn tại." });

                profile = new UserProfile
                {
                    UserId = request.UserId,
                    FullName = request.FullName,
                    Address = request.Address,
                    DateOfBirth = request.DateOfBirth,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userOperations.AddUserProfileAsync(profile, cancellationToken);
            }
            else
            {
                profile.FullName = request.FullName;
                profile.Address = request.Address;
                profile.DateOfBirth = request.DateOfBirth;
                profile.UpdatedAt = DateTime.UtcNow;
                await _userOperations.UpdateUserProfileAsync(profile, cancellationToken);
            }

            return new OkObjectResult(new { message = "Cập nhật hồ sơ thành công." });
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            return new BadRequestObjectResult(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
        }
    }
}
