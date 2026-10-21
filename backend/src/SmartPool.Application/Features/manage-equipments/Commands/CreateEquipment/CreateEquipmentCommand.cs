using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;

public sealed class CreateEquipmentCommand : IRequest<EquipmentListItem>
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public JsonElement? Metadata { get; set; }
    public CameraConnectionConfiguration? CameraConnection { get; set; }
}

public sealed class CreateEquipmentValidator : AbstractValidator<CreateEquipmentCommand>
{
    public CreateEquipmentValidator()
    {
        RuleFor(x => x.Metadata).Custom((value, context) =>
        {
            var error = EquipmentSpecifications.ValidationError(value);
            if (error != null) context.AddFailure(error);
        });
        When(x => CameraMetadata.IsCamera(x.Category), () =>
        {
            RuleFor(x => x.Metadata).Must(value => value == null || !CameraMetadata.ContainsConnectionCredentials(value.Value))
                .WithMessage("Không nhập password, credentials hoặc URL có thông tin đăng nhập vào thông số riêng. Sử dụng phần cấu hình camera.");
            RuleFor(x => x.CameraConnection).NotNull().WithMessage("Nhập cấu hình kết nối camera.");
            When(x => x.CameraConnection != null, () =>
            {
                RuleFor(x => x.CameraConnection!.Protocol).Must(value => value is "RTSP" or "Browser").WithMessage("Chọn nguồn camera hợp lệ.");
                When(x => x.CameraConnection!.Protocol == "RTSP", () =>
                {
                    RuleFor(x => x.CameraConnection!.StreamUrl).Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    uri.Scheme == "rtsp" && uri.Port != 0 && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && !string.IsNullOrWhiteSpace(uri.Host) &&
                    !value.Any(char.IsControl)).WithMessage("Nhập RTSP URL hợp lệ, không nhúng username/password vào URL.");
                    RuleFor(x => x.CameraConnection!.Username).NotEmpty().When(x => !string.IsNullOrEmpty(x.CameraConnection!.Password))
                    .WithMessage("Nhập username khi sử dụng password.");
                });
                When(x => x.CameraConnection!.Protocol == "Browser", () =>
                {
                    RuleFor(x => x.CameraConnection!.StreamUrl).Empty().WithMessage("Camera máy tính không sử dụng địa chỉ RTSP.");
                    RuleFor(x => x.CameraConnection!.Username).Empty().WithMessage("Camera máy tính không sử dụng tài khoản RTSP.");
                    RuleFor(x => x.CameraConnection!.Password).Empty().WithMessage("Camera máy tính không sử dụng mật khẩu RTSP.");
                });
            });
        });
        RuleFor(x => x.CameraConnection).Null().When(x => !CameraMetadata.IsCamera(x.Category))
            .WithMessage("Cấu hình camera chỉ áp dụng cho nhóm Camera.");
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Tên thiết bị không được để trống.")
            .Must(value => value.Trim().Length <= 255).WithMessage("Tên thiết bị không được vượt quá 255 ký tự.")
            .Must(value => !value.Contains('\0')).WithMessage("Tên thiết bị chứa ký tự không hợp lệ.");
        RuleFor(x => x.Category).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Nhóm thiết bị không được để trống.")
            .Must(value => value.Trim().Length <= 50).WithMessage("Nhóm thiết bị không được vượt quá 50 ký tự.")
            .Must(value => !value.Contains('\0')).WithMessage("Nhóm thiết bị chứa ký tự không hợp lệ.");
        RuleFor(x => x.Location)
            .Must(value => value == null || value.Trim().Length <= 255).WithMessage("Vị trí không được vượt quá 255 ký tự.")
            .Must(value => value == null || !value.Contains('\0')).WithMessage("Vị trí chứa ký tự không hợp lệ.");
    }
}
