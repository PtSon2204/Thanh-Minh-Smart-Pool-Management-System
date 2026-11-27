using MediatR;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageStaffs.Commands.UploadStaffAvatar;

public sealed class UploadStaffAvatarCommand : IRequest<UploadStaffAvatarResponse>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public IFormFile File { get; set; } = null!;
}

public sealed record UploadStaffAvatarResponse(string AvatarUrl);
