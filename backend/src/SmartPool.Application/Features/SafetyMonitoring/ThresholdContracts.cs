using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.SafetyMonitoring;

/// <summary>Ngưỡng cảnh báo AI hiệu lực (10 thông số chỉnh được từ giao diện).</summary>
public sealed record SafetyThresholdsDto(
    double ConfidenceThreshold,
    double WarningRatio,
    double CriticalRatio,
    double RecoveryRatio,
    double CriticalHoldSeconds,
    double RecoveryHoldSeconds,
    double CooldownSeconds,
    double WindowSeconds,
    double MaxObservationGapSeconds,
    int MinObservations,
    int Version,
    string Origin,
    Guid? UpdatedBy = null,
    DateTime? UpdatedAt = null)
{
    public RiskOptions ToOptions() => new()
    {
        ConfidenceThreshold = ConfidenceThreshold,
        WarningRatio = WarningRatio,
        CriticalRatio = CriticalRatio,
        RecoveryRatio = RecoveryRatio,
        CriticalHoldSeconds = CriticalHoldSeconds,
        RecoveryHoldSeconds = RecoveryHoldSeconds,
        CooldownSeconds = CooldownSeconds,
        WindowSeconds = WindowSeconds,
        MaxObservationGapSeconds = MaxObservationGapSeconds,
        MinObservations = MinObservations,
    };
}

public static class SafetyThresholdsDtoExtensions
{
    public static SafetyThresholdsDto ToDto(this RiskOptions o, int version, string origin, Guid? updatedBy = null, DateTime? updatedAt = null) =>
        new(o.ConfidenceThreshold, o.WarningRatio, o.CriticalRatio, o.RecoveryRatio, o.CriticalHoldSeconds,
            o.RecoveryHoldSeconds, o.CooldownSeconds, o.WindowSeconds, o.MaxObservationGapSeconds, o.MinObservations,
            version, origin, updatedBy, updatedAt);
}

/// <summary>
/// Nguồn ngưỡng AI dùng chung: instance <see cref="RiskOptions"/> đang hiệu lực (được
/// engine đọc live từng khung hình) và khả năng đọc/lưu bảng single-row trong DB.
/// </summary>
public interface IRiskThresholds
{
    /// <summary>Instance RiskOptions sống, chia sẻ với mọi engine — mutate in-place để đổi ngưỡng nóng.</summary>
    RiskOptions Current { get; }

    /// <summary>Áp bảng DB (nếu có) lên <see cref="Current"/> khi khởi động; DB lỗi thì giữ giá trị appsettings.</summary>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>Giá trị hiệu lực hiện tại: bảng DB (Origin=DATABASE) hoặc appsettings (Origin=APPSETTINGS).</summary>
    Task<SafetyThresholdsDto> GetEffectiveAsync(CancellationToken ct);

    /// <summary>Upsert bảng single-row, tăng dồn version và áp lên <see cref="Current"/> (nóng, không reset state engine).</summary>
    Task<SafetyThresholdsDto> SaveAsync(SafetyThresholdsDto input, Guid updatedBy, CancellationToken ct);
}

public sealed record GetThresholdsQuery : IRequest<SafetyThresholdsDto>;
public sealed class GetThresholdsHandler(IRiskThresholds thresholds) : IRequestHandler<GetThresholdsQuery, SafetyThresholdsDto>
{
    public Task<SafetyThresholdsDto> Handle(GetThresholdsQuery request, CancellationToken ct)
        => thresholds.GetEffectiveAsync(ct);
}

public sealed record SaveThresholdsCommand(
    double ConfidenceThreshold,
    double WarningRatio,
    double CriticalRatio,
    double RecoveryRatio,
    double CriticalHoldSeconds,
    double RecoveryHoldSeconds,
    double CooldownSeconds,
    double WindowSeconds,
    double MaxObservationGapSeconds,
    int MinObservations) : IRequest<SafetyThresholdsDto>;

public sealed class SaveThresholdsHandler(IRiskThresholds thresholds, ICurrentUser user) : IRequestHandler<SaveThresholdsCommand, SafetyThresholdsDto>
{
    public Task<SafetyThresholdsDto> Handle(SaveThresholdsCommand request, CancellationToken ct)
        => thresholds.SaveAsync(new(request.ConfidenceThreshold, request.WarningRatio, request.CriticalRatio, request.RecoveryRatio,
            request.CriticalHoldSeconds, request.RecoveryHoldSeconds, request.CooldownSeconds, request.WindowSeconds,
            request.MaxObservationGapSeconds, request.MinObservations, Version: 0, Origin: "DATABASE"),
            user.UserId ?? throw new UnauthorizedAccessException(), ct);
}

public sealed class SaveThresholdsCommandValidator : FluentValidation.AbstractValidator<SaveThresholdsCommand>
{
    public SaveThresholdsCommandValidator()
    {
        // Phải khớp RiskOptions.IsValid(): tỷ lệ hồi phục < cảnh báo < khẩn cấp; độ tin cậy 0..1.
        RuleFor(x => x.ConfidenceThreshold).Must(v => v is >= 0 and <= 1).WithMessage("Độ tin cậy phải từ 0 đến 1.");
        RuleFor(x => x.WarningRatio).Must(v => v is >= 0 and <= 1).WithMessage("Tỷ lệ cảnh báo phải từ 0 đến 1.");
        RuleFor(x => x.CriticalRatio).Must(v => v is >= 0 and <= 1).WithMessage("Tỷ lệ khẩn cấp phải từ 0 đến 1.");
        RuleFor(x => x.RecoveryRatio).Must((x, v) => v >= 0 && v < x.WarningRatio).WithMessage("Tỷ lệ hồi phục phải không âm và nhỏ hơn tỷ lệ cảnh báo.");
        RuleFor(x => x.WarningRatio).Must((x, v) => v < x.CriticalRatio).WithMessage("Tỷ lệ cảnh báo phải nhỏ hơn tỷ lệ khẩn cấp.");
        RuleFor(x => x.WindowSeconds).Must(v => v > 0).WithMessage("Cửa sổ quan sát phải lớn hơn 0 giây.");
        RuleFor(x => x.MaxObservationGapSeconds).Must(v => v > 0).WithMessage("Khoảng trống quan sát phải lớn hơn 0 giây.");
        RuleFor(x => x.CriticalHoldSeconds).Must(v => v > 0).WithMessage("Thời gian giữ khẩn cấp phải lớn hơn 0 giây.");
        RuleFor(x => x.RecoveryHoldSeconds).Must(v => v > 0).WithMessage("Thời gian giữ hồi phục phải lớn hơn 0 giây.");
        RuleFor(x => x.CooldownSeconds).Must(v => v >= 0).WithMessage("Khoảng cooldown không được âm.");
        RuleFor(x => x.MinObservations).Must(v => v >= 2).WithMessage("Số quan sát tối thiểu phải từ 2.");
    }
}
