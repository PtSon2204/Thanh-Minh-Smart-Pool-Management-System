using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageCoachApplications;

public sealed record SubmitCoachApplicationCommand(IFormFile? Certificate)
    : IRequest<CoachApplicationItem>;

public sealed class SubmitCoachApplicationValidator : AbstractValidator<SubmitCoachApplicationCommand>
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    public SubmitCoachApplicationValidator()
    {
        RuleFor(command => command.Certificate)
            .Must(file => file is not null && file.Length is >= 4 and <= MaxImageBytes &&
                          file.ContentType is "image/jpeg" or "image/png")
            .WithMessage("Chỉ nhận ảnh JPG/PNG tối đa 5 MB.");
    }
}

public sealed class SubmitCoachApplicationHandler(
    ICoachApplicationOperations operations,
    ICurrentUser currentUser,
    IValidator<SubmitCoachApplicationCommand> validator)
    : IRequestHandler<SubmitCoachApplicationCommand, CoachApplicationItem>
{
    public async Task<CoachApplicationItem> Handle(
        SubmitCoachApplicationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            !currentUser.IsInRole("CUSTOMER") || currentUser.IsInRole("ADMIN"))
            throw new UnauthorizedAccessException("Chỉ Customer được gửi yêu cầu Coach.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await using var stream = request.Certificate!.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var format = DetectFormat(bytes);
        if (bytes.Length > SubmitCoachApplicationValidator.MaxImageBytes || format is null ||
            (format == "jpg" && request.Certificate.ContentType != "image/jpeg") ||
            (format == "png" && request.Certificate.ContentType != "image/png"))
            throw new ValidationException([new ValidationFailure(nameof(request.Certificate),
                "Nội dung ảnh không khớp định dạng JPG/PNG.")]);

        return await operations.SubmitAsync(userId, bytes, format, cancellationToken);
    }

    private static string? DetectFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        return bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 &&
               bytes[^2] == 0xFF && bytes[^1] == 0xD9 ? "jpg" : null;
    }
}
