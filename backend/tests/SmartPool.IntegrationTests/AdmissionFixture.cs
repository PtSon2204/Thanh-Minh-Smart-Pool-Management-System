using AutoMapper;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

internal sealed class AdmissionFixture : IAsyncDisposable
{
    private readonly IMapper _mapper;
    private readonly DbContextOptions<SmartPoolDbContext> _options;
    private readonly HashSet<Guid> _ticketIds = [];
    private readonly HashSet<Guid> _ticketTypeIds = [];
    private bool _setupComplete;
    private bool _cleaned;

    private AdmissionFixture(DbContextOptions<SmartPoolDbContext> options)
    {
        _options = options;
        _mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        TicketTypeId = Guid.NewGuid();
        TicketId = Guid.NewGuid();
        QrCode = $"QA_MONTHLY_{Guid.NewGuid():N}";
        _ticketIds.Add(TicketId);
        _ticketTypeIds.Add(TicketTypeId);
    }

    public Guid TicketTypeId { get; }
    public Guid TicketId { get; }
    public Guid OperatorId { get; private set; }
    public string QrCode { get; }
    public SmartPoolDbContext Context { get; private set; } = null!;
    public (int Logs, int Tickets, int TicketTypes)? CleanupCounts { get; private set; }

    public static async Task<AdmissionFixture> CreateAsync()
    {
        LoadPrivateDatabaseConfiguration();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Integration tests require the private backend database configuration.");
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || connection.Port != 5432
            || !string.Equals(connection.Database, "smartpool_dev", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Integration test database guard rejected the configured target.");
        }

        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var fixture = new AdmissionFixture(options) { Context = new SmartPoolDbContext(options) };
        try
        {
            await fixture.Context.Database.OpenConnectionAsync();
            await fixture.Context.Database.CloseConnectionAsync();
            fixture.OperatorId = await fixture.Context.Users.AsNoTracking()
                .Select(user => user.Id)
                .FirstOrDefaultAsync();
            if (fixture.OperatorId == Guid.Empty)
            {
                throw new InvalidOperationException("The guarded test database has no existing user for the operator foreign key.");
            }

            fixture.Context.TicketTypes.Add(new TicketType
            {
                Id = fixture.TicketTypeId,
                Name = $"QA monthly {fixture.TicketTypeId:N}",
                TicketCategory = "VE_THANG",
                Price = 1,
                DurationDays = 30,
                IsActive = true,
                IsDeleted = false
            });
            fixture.Context.Tickets.Add(fixture.CreateTicket(fixture.TicketId, fixture.QrCode));
            await fixture.Context.SaveChangesAsync();
            fixture._setupComplete = true;
            return fixture;
        }
        catch
        {
            await fixture.CleanupAsync();
            await fixture.Context.DisposeAsync();
            throw;
        }
    }

    public SmartPoolDbContext CreateContext() => new(_options);

    public PoolAccessOperations CreateOperations(DateTimeOffset instant) =>
        CreateOperations(Context, instant);

    public PoolAccessOperations CreateOperations(SmartPoolDbContext context, DateTimeOffset instant) =>
        new(context, _mapper, new FixedTimeProvider(instant));

    public async Task<(Guid Id, string QrCode)> AddTicketAsync()
    {
        var id = Guid.NewGuid();
        var qrCode = $"QA_MONTHLY_{Guid.NewGuid():N}";
        _ticketIds.Add(id);
        Context.Tickets.Add(CreateTicket(id, qrCode));
        await Context.SaveChangesAsync();
        return (id, qrCode);
    }

    public async Task<(Guid Id, string QrCode)> AddSingleUseTicketWithLegacyBalanceAsync(int remainingEntries)
    {
        var ticketTypeId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var qrCode = $"QA_SINGLE_USE_{Guid.NewGuid():N}";
        _ticketTypeIds.Add(ticketTypeId);
        _ticketIds.Add(ticketId);

        Context.TicketTypes.Add(new TicketType
        {
            Id = ticketTypeId,
            Name = $"QA single use {ticketTypeId:N}",
            TicketCategory = "VE_LUOT",
            Price = 1,
            IsActive = true,
            IsDeleted = false
        });
        Context.Tickets.Add(CreateTicket(ticketId, qrCode, ticketTypeId));
        await Context.SaveChangesAsync();
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE smart_pool.tickets SET remaining_entries = {remainingEntries} WHERE id = {ticketId}");
        if (await ReadRemainingEntriesAsync(ticketId) != remainingEntries)
        {
            throw new InvalidOperationException("QA fixture legacy balance did not persist on its new ticket.");
        }

        return (ticketId, qrCode);
    }

    public async Task<int> ReadRemainingEntriesAsync(Guid ticketId)
    {
        return await Context.Database.SqlQuery<int>(
                $"SELECT remaining_entries AS \"Value\" FROM smart_pool.tickets WHERE id = {ticketId}")
            .SingleAsync();
    }

    public async Task AddLogAsync(string status, DateTime scanTime)
    {
        Context.EntryLogs.Add(new EntryLog
        {
            Id = Guid.NewGuid(),
            TicketId = TicketId,
            ScanTime = scanTime,
            Status = status,
            Message = "QA fixture",
            InputMode = "Qr"
        });
        await Context.SaveChangesAsync();
    }

    public async Task<List<EntryLog>> ReadTicketLogsAsync(Guid? ticketId = null)
    {
        await using var query = new SmartPoolDbContext(_options);
        return await query.EntryLogs.AsNoTracking()
            .Where(log => log.TicketId == (ticketId ?? TicketId))
            .OrderBy(log => log.ScanTime)
            .ToListAsync();
    }

    public async Task CleanupAsync()
    {
        if (_cleaned)
        {
            return;
        }

        await using var cleanup = new SmartPoolDbContext(_options);
        var fixtureLogs = cleanup.EntryLogs
            .Where(log => log.TicketId != null && _ticketIds.Contains(log.TicketId.Value));
        var logCount = await fixtureLogs.CountAsync();
        var deletedLogs = await fixtureLogs.ExecuteDeleteAsync();
        var deletedTickets = await cleanup.Tickets.Where(ticket => _ticketIds.Contains(ticket.Id)).ExecuteDeleteAsync();
        var deletedTypes = await cleanup.TicketTypes.Where(ticketType => _ticketTypeIds.Contains(ticketType.Id)).ExecuteDeleteAsync();

        if (_setupComplete)
        {
            if (deletedLogs != logCount || deletedTickets != _ticketIds.Count || deletedTypes != _ticketTypeIds.Count)
            {
                throw new InvalidOperationException("QA fixture cleanup did not delete all of its exact created rows.");
            }
        }
        CleanupCounts = (deletedLogs, deletedTickets, deletedTypes);
        _cleaned = true;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
        await Context.DisposeAsync();
    }

    private Ticket CreateTicket(Guid id, string qrCode, Guid? ticketTypeId = null) => new()
    {
        Id = id,
        TicketTypeId = ticketTypeId ?? TicketTypeId,
        UserId = OperatorId,
        QrCode = qrCode,
        IssueDate = DateTime.SpecifyKind(new DateTime(2026, 10, 1), DateTimeKind.Utc),
        ExpiryDate = DateTime.SpecifyKind(new DateTime(2026, 11, 1), DateTimeKind.Utc),
        Status = PoolAccessValues.Active,
        IsDeleted = false
    };

    private static void LoadPrivateDatabaseConfiguration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SmartPool.API", ".env");
            if (File.Exists(candidate))
            {
                Env.Load(candidate);
                return;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Private backend .env configuration is unavailable.");
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}
