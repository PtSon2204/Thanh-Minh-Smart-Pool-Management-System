using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;

public sealed record GetCameraListQuery(string? Search = null, string? Status = null, string? Area = null,
    int Page = 1, int PageSize = 4, Guid? CameraId = null) : IRequest<PagedResponse<CameraListItem>>;

public sealed class CameraListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string ConnectionStatus { get; set; } = "Disconnected";
    public string Source { get; set; } = "RTSP";
    public bool FastReconnect { get; set; }
    public bool SmoothPlayback { get; set; }
}

public interface ICameraListRepository
{
    Task<PagedResponse<CameraListItem>> ListAsync(GetCameraListQuery query, CancellationToken ct);
    Task<string[]> AreasAsync(CancellationToken ct);
}

public sealed class GetCameraListValidator : AbstractValidator<GetCameraListQuery>
{
    public static string NormalizeSearch(string? value) => new((value ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());

    public GetCameraListValidator()
    {
        RuleFor(x => x.Search).Must(value => NormalizeSearch(value).Length <= 200)
            .WithMessage("Tên hoặc vị trí tìm kiếm tối đa 200 ký tự, không tính khoảng trắng.")
            .Must(value => NormalizeSearch(value) is var term && (term.Length == 0 || term.Count(char.IsLetterOrDigit) >= 2))
            .WithMessage("Vui lòng nhập ít nhất 2 chữ cái hoặc chữ số, không tính khoảng trắng và ký tự đặc biệt.");
        RuleFor(x => x.Status).MaximumLength(50);
        RuleFor(x => x.Area).MaximumLength(200);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 16);
    }
}

public sealed class GetCameraListHandler(ICameraListRepository repository, ICameraService cameras,
    IValidator<GetCameraListQuery> validator) : IRequestHandler<GetCameraListQuery, PagedResponse<CameraListItem>>
{
    public async Task<PagedResponse<CameraListItem>> Handle(GetCameraListQuery request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var result = await repository.ListAsync(request, ct);
        foreach (var item in result.Items)
        {
            var status = cameras.GetStatus(item.Id);
            item.ConnectionStatus = item.Source == "Browser" ? "Unavailable" : status.State;
            item.FastReconnect = item.Source == "RTSP" && status.AnalysisSession != null;
            item.SmoothPlayback = item.Source == "RTSP" && status.SmoothPlayback;
        }
        return result;
    }
}

public sealed record GetCameraAreasQuery : IRequest<string[]>;
public sealed class GetCameraAreasHandler(ICameraListRepository repository) : IRequestHandler<GetCameraAreasQuery, string[]>
{
    public Task<string[]> Handle(GetCameraAreasQuery request, CancellationToken ct) => repository.AreasAsync(ct);
}
