using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed class MaintenanceOperations(SmartPoolDbContext db, TimeProvider clock,
    IValidator<MaintenanceRequest> validator) : IMaintenanceOperations
{
    // Shared by HTTP mutations and the worker across API instances. No schema changes are needed.
    private const long MaintenanceLock = 687216658453287;
    private DateOnly Today => VietnamTimeBoundary.GetDate(clock);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private IQueryable<User> EligibleUsers() => db.Users.Where(x => x.IsDeleted != true && x.Status != null &&
        x.Status.ToUpper() == "ACTIVE" && x.Role != null && x.Role.IsDeleted != true &&
        (x.Role.Name.ToUpper() == "ADMIN" || (x.Employee != null && x.Employee.Status == "Working")));

    private IQueryable<MaintenanceSchedule> Schedules() => db.MaintenanceSchedules
        .Include(x => x.Equipment).Include(x => x.AssignedToNavigation).ThenInclude(x => x!.UserProfile);

    public async Task<MaintenanceOptions> GetOptionsAsync(CancellationToken ct)
    {
        var equipment = await db.Equipments.AsNoTracking().Where(x => x.IsDeleted != true).OrderBy(x => x.Name)
            .Select(x => new MaintenanceOption(x.Id, x.Name + (x.Location == null || x.Location == "" ? "" : " — " + x.Location)))
            .ToListAsync(ct);
        var people = await EligibleUsers().AsNoTracking().OrderBy(x => x.UserProfile != null ? x.UserProfile.FullName : x.Username)
            .Select(x => new MaintenanceOption(x.Id, (x.UserProfile != null ? x.UserProfile.FullName : x.Username) ?? "Chưa có tên"))
            .ToListAsync(ct);
        return new MaintenanceOptions(equipment, people);
    }

    public async Task<MaintenanceList> GetListAsync(MaintenanceListQuery query, CancellationToken ct)
    {
        ValidateQuery(query);
        var source = Schedules().AsNoTracking().Where(x => x.Equipment.IsDeleted != true);
        if (query.EquipmentId.HasValue) source = source.Where(x => x.EquipmentId == query.EquipmentId.Value);
        var rows = (await source.OrderBy(x => x.PlannedDate).ThenBy(x => x.Id).ToListAsync(ct)).Select(Item).ToList();
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            rows = rows.Where(x => x.TaskName.Contains(query.SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase) ||
                x.EquipmentName.Contains(query.SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var summary = new MaintenanceSummary(rows.Count, rows.Count(x => x.Status == "Pending"),
            rows.Count(x => x.Status == "InProgress"), rows.Count(x => x.IsOverdue));
        if (!string.IsNullOrEmpty(query.Status))
            rows = rows.Where(x => query.Status == "Overdue" ? x.IsOverdue : x.Status == query.Status).ToList();
        return new MaintenanceList(Page(rows, query), summary);
    }

    public Task<MaintenanceItem> CreateAsync(MaintenanceRequest request, CancellationToken ct) => WriteAsync(async () =>
    {
        await ValidateRequest(request, null, ct);
        var row = new MaintenanceSchedule { Id = Guid.NewGuid(), EquipmentId = request.EquipmentId,
            AssignedTo = request.AssignedTo, PlannedDate = request.StartDate, Status = "Pending", CreatedAt = Now, UpdatedAt = Now };
        row.Description = Configuration(request, row.Id, 0).Serialize();
        db.MaintenanceSchedules.Add(row);
        await db.SaveChangesAsync(ct);
        return Item(await Schedules().SingleAsync(x => x.Id == row.Id, ct));
    }, ct);

    public Task<MaintenanceItem> UpdateAsync(Guid id, MaintenanceRequest request, CancellationToken ct) => WriteAsync(async () =>
    {
        var row = await Find(id, ct);
        if (row.Status is not ("Pending" or "Paused")) throw Conflict("Chỉ cập nhật lịch đang chờ hoặc tạm dừng.");
        await ValidateRequest(request, row.PlannedDate, ct);
        var old = MaintenanceConfiguration.Read(row);
        row.EquipmentId = request.EquipmentId;
        row.AssignedTo = request.AssignedTo;
        row.PlannedDate = request.StartDate;
        row.Description = Configuration(request, old.SeriesId, old.Sequence).Serialize();
        row.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return Item(await Schedules().AsNoTracking().SingleAsync(x => x.Id == row.Id, ct));
    }, ct);

    public async Task ChangeStateAsync(Guid id, string action, CancellationToken ct)
    {
        await WriteAsync(async () =>
        {
            var row = await Find(id, ct);
            switch (action)
            {
                case "start" when row.Status == "Pending":
                    if (row.PlannedDate > Today) throw Conflict("Chưa đến ngày thực hiện. Hãy cập nhật ngày bắt đầu nếu cần làm sớm.");
                    if (await db.MaintenanceSchedules.AnyAsync(x => x.Id != id && x.EquipmentId == row.EquipmentId && x.Status == "InProgress", ct))
                        throw Conflict("Thiết bị đang có công việc bảo trì khác được thực hiện.");
                    var config = MaintenanceConfiguration.Read(row) with { PreviousEquipmentStatus = row.Equipment.Status };
                    row.Description = config.Serialize();
                    row.Status = "InProgress";
                    // Conditional SQL avoids overwriting a lock applied concurrently from the equipment page.
                    await db.Equipments.Where(x => x.Id == row.EquipmentId && x.Status == "Active")
                        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Maintenance").SetProperty(x => x.UpdatedAt, Now), ct);
                    break;
                case "pause" when row.Status == "Pending": row.Status = "Paused"; break;
                case "resume" when row.Status == "Paused": row.Status = "Pending"; break;
                case "cancel" when row.Status is "Pending" or "Paused": row.Status = "Cancelled"; break;
                default: throw Conflict("Thao tác không phù hợp với trạng thái hiện tại. Vui lòng tải lại danh sách.");
            }
            row.UpdatedAt = Now;
            return true;
        }, ct);
    }

    public async Task CompleteAsync(Guid id, CompleteMaintenanceRequest request, Guid performedBy, CancellationToken ct)
    {
        if (request.Cost < 0 || request.Cost > 9999999999999999.99m) Invalid("Cost", "Chi phí không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 4000) Invalid("Note", "Nhập kết quả thực hiện, tối đa 4000 ký tự.");
        if (request.VendorName?.Trim().Length > 255) Invalid("VendorName", "Tên đơn vị bảo trì tối đa 255 ký tự.");
        await WriteAsync(async () =>
        {
            var row = await Find(id, ct);
            var logged = await db.MaintenanceLogs.AnyAsync(x => x.ScheduleId == id, ct);
            if (row.Status == "Completed" && logged) return true;
            if (row.Status != "InProgress" || logged) throw Conflict("Chỉ hoàn thành công việc đang thực hiện và chưa có kết quả.");
            var config = MaintenanceConfiguration.Read(row);
            db.MaintenanceLogs.Add(new MaintenanceLog { Id = Guid.NewGuid(), EquipmentId = row.EquipmentId,
                ScheduleId = row.Id, ActualDate = Now, Cost = request.Cost, PerformedBy = performedBy,
                VendorName = string.IsNullOrWhiteSpace(request.VendorName) ? null : request.VendorName.Trim(),
                Note = (request.ReadyForUse ? "Kết quả: Đủ điều kiện sử dụng.\n" : "Kết quả: Cần kiểm tra trước khi sử dụng lại.\n") + request.Note.Trim(), CreatedAt = Now });
            row.Status = "Completed";
            row.UpdatedAt = Now;
            // An independently locked device must never be automatically unlocked by maintenance.
            if (request.ReadyForUse)
            {
                await db.Equipments.Where(x => x.Id == row.EquipmentId && x.Status == "Maintenance")
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Active").SetProperty(x => x.UpdatedAt, Now), ct);
            }
            var siblings = await db.MaintenanceSchedules.AsNoTracking().Where(x => x.Id != id).ToListAsync(ct);
            AddNext(row, config, siblings);
            return true;
        }, ct);
    }

    public async Task<PagedResponse<MaintenanceHistoryItem>> GetHistoryAsync(MaintenanceListQuery query, CancellationToken ct)
    {
        ValidateQuery(query);
        var source = db.MaintenanceLogs.AsNoTracking().Include(x => x.Equipment).Include(x => x.Schedule)
            .Include(x => x.PerformedByNavigation).ThenInclude(x => x!.UserProfile).AsQueryable();
        if (query.EquipmentId.HasValue) source = source.Where(x => x.EquipmentId == query.EquipmentId.Value);
        var rows = (await source.OrderByDescending(x => x.ActualDate).ThenBy(x => x.Id).ToListAsync(ct))
            .Select(x => new MaintenanceHistoryItem(x.Id, x.ScheduleId, x.EquipmentId, x.Equipment.Name,
                x.Schedule == null ? "Bảo trì phát sinh" : MaintenanceConfiguration.Read(x.Schedule).TaskName,
                x.Schedule?.PlannedDate, x.ActualDate, x.Cost ?? 0,
                x.PerformedByNavigation?.UserProfile?.FullName ?? x.PerformedByNavigation?.Username ?? "Chưa xác định", x.VendorName, x.Note)).ToList();
        if (!string.IsNullOrWhiteSpace(query.SearchTerm)) rows = rows.Where(x =>
            x.EquipmentName.Contains(query.SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase) ||
            x.TaskName.Contains(query.SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return Page(rows, query);
    }

    public async Task SynchronizeAsync(CancellationToken ct)
    {
        await WriteAsync(async () =>
        {
            var schedules = await Schedules().Where(x => x.Equipment.IsDeleted != true).ToListAsync(ct);
            foreach (var group in schedules.GroupBy(x => MaintenanceConfiguration.Read(x).SeriesId))
            {
                var last = group.OrderByDescending(x => MaintenanceConfiguration.Read(x).Sequence).First();
                if (last.Status == "Completed") AddNext(last, MaintenanceConfiguration.Read(last), schedules);
            }
            // Generated occurrences join this tick's reminder scan after saving, inside the same lock.
            await db.SaveChangesAsync(ct);
            var active = await Schedules().Where(x => x.Equipment.IsDeleted != true && (x.Status == "Pending" || x.Status == "InProgress")).ToListAsync(ct);
            var users = await db.Users.AsNoTracking().Where(x => x.IsDeleted != true && x.Status != null && x.Status.ToUpper() == "ACTIVE")
                .Select(x => new { x.Id, IsAdmin = x.Role != null && x.Role.IsDeleted != true && x.Role.Name.ToUpper() == "ADMIN" }).ToListAsync(ct);
            var activeIds = users.Select(x => x.Id).ToHashSet();
            foreach (var row in active)
            {
                var config = MaintenanceConfiguration.Read(row);
                var days = row.PlannedDate.DayNumber - Today.DayNumber;
                if (days > config.RemindBeforeDays) continue;
                var stage = days < 0 ? "O" : days == 0 ? "D" : "L";
                var type = $"MT:{row.Id:N}:{row.PlannedDate:yyyyMMdd}:{stage}";
                var recipients = users.Where(x => x.IsAdmin).Select(x => x.Id).ToHashSet();
                if (row.AssignedTo.HasValue && activeIds.Contains(row.AssignedTo.Value)) recipients.Add(row.AssignedTo.Value);
                foreach (var userId in recipients)
                {
                    if (await db.Notifications.AnyAsync(x => x.UserId == userId && x.Type == type, ct)) continue;
                    db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Type = type,
                        Title = days < 0 ? "Bảo trì thiết bị quá hạn" : days == 0 ? "Đến hạn bảo trì thiết bị" : "Sắp đến lịch bảo trì thiết bị",
                        Content = $"{config.TaskName} — {row.Equipment.Name}. Ngày thực hiện: {row.PlannedDate:dd/MM/yyyy}. Vào Cấu hình lịch bảo trì để xem và xử lý.",
                        IsRead = false, CreatedAt = Now });
                }
            }
            return true;
        }, ct);
    }

    private void AddNext(MaintenanceSchedule row, MaintenanceConfiguration config, IEnumerable<MaintenanceSchedule> existing)
    {
        if (config.RepeatUnit == "None" || existing.Any(x =>
            MaintenanceConfiguration.Read(x) is var other && other.SeriesId == config.SeriesId && other.Sequence > config.Sequence)) return;
        var next = config.NextAfter(Today);
        if (!next.HasValue) return;
        db.MaintenanceSchedules.Add(new MaintenanceSchedule { Id = Guid.NewGuid(), EquipmentId = row.EquipmentId,
            AssignedTo = row.AssignedTo, PlannedDate = next.Value.Date, Status = "Pending", CreatedAt = Now, UpdatedAt = Now,
            Description = (config with { Sequence = next.Value.Sequence, PreviousEquipmentStatus = null }).Serialize() });
    }

    private async Task ValidateRequest(MaintenanceRequest request, DateOnly? previousDate, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.StartDate < Today && request.StartDate != previousDate) Invalid("StartDate", "Ngày bắt đầu không được trước hôm nay.");
        if (!await db.Equipments.AnyAsync(x => x.Id == request.EquipmentId && x.IsDeleted != true, ct)) Invalid("EquipmentId", "Thiết bị không tồn tại hoặc đã bị xóa.");
        if (!await EligibleUsers().AnyAsync(x => x.Id == request.AssignedTo, ct)) Invalid("AssignedTo", "Chọn nhân sự đang hoạt động để phụ trách công việc.");
    }

    private async Task<MaintenanceSchedule> Find(Guid id, CancellationToken ct) =>
        await Schedules().SingleOrDefaultAsync(x => x.Id == id && x.Equipment.IsDeleted != true, ct)
        ?? throw new MaintenanceException(404, "Không tìm thấy lịch bảo trì.");

    private MaintenanceItem Item(MaintenanceSchedule row)
    {
        var config = MaintenanceConfiguration.Read(row);
        return new MaintenanceItem(row.Id, row.EquipmentId, row.Equipment.Name, row.Equipment.Location, config.TaskName,
            config.RepeatUnit, config.RepeatEvery, row.PlannedDate, row.AssignedTo,
            row.AssignedToNavigation?.UserProfile?.FullName ?? row.AssignedToNavigation?.Username ?? "Chưa phân công",
            config.RemindBeforeDays, row.Status ?? "Pending", row.PlannedDate < Today && row.Status is "Pending" or "InProgress",
            config.NextAfter(row.PlannedDate > Today ? row.PlannedDate : Today)?.Date);
    }

    private static MaintenanceConfiguration Configuration(MaintenanceRequest request, Guid seriesId, int sequence) => new()
    {
        SeriesId = seriesId, Sequence = sequence, AnchorSequence = sequence, AnchorDate = request.StartDate,
        TaskName = request.TaskName.Trim(), RepeatUnit = request.RepeatUnit,
        RepeatEvery = request.RepeatUnit == "None" ? 1 : request.RepeatEvery, RemindBeforeDays = request.RemindBeforeDays
    };

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({MaintenanceLock})", ct);
        var result = await action();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static void ValidateQuery(MaintenanceListQuery query)
    {
        if (query.PageIndex < 1 || query.PageIndex > int.MaxValue / 100 || query.PageSize is < 1 or > 100)
            throw new MaintenanceException(400, "Thông tin phân trang không hợp lệ.");
        if (query.SearchTerm?.Length > 255) throw new MaintenanceException(400, "Từ khóa tìm kiếm tối đa 255 ký tự.");
        if (query.Status is not (null or "" or "Pending" or "InProgress" or "Completed" or "Cancelled" or "Paused" or "Overdue"))
            throw new MaintenanceException(400, "Trạng thái lọc không hợp lệ.");
    }

    private static PagedResponse<T> Page<T>(List<T> rows, MaintenanceListQuery query) => new()
    {
        Items = rows.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToList(),
        TotalCount = rows.Count, PageIndex = query.PageIndex, PageSize = query.PageSize
    };

    private static MaintenanceException Conflict(string message) => new(409, message);
    private static void Invalid(string field, string message) => throw new ValidationException([new ValidationFailure(field, message)]);
}
