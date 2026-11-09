using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPool.API.Controllers;
using SmartPool.API.Extensions;
using SmartPool.Application;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.ManageTickets;

public sealed class TicketManagementApiTests
{
    [Fact]
    [Trait("TestCaseId", "TM-TT-01")]
    public async Task AdminCanCreateReadUpdateLockAndSoftDeleteTicketType()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");

        var created = await fixture.Client.PostAsJsonAsync("/api/ticket-types", new
        {
            name = fixture.Name("Monthly"), ticketCategory = "VE_THANG", price = 120000,
            durationDays = 30
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var typeId = createdBody.RootElement.GetProperty("id").GetGuid();
        var typeName = createdBody.RootElement.GetProperty("name").GetString();
        fixture.TrackTicketType(typeId);

        var read = await fixture.Client.GetAsync($"/api/ticket-types/{typeId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var readBody = System.Text.Json.JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(typeName, readBody.RootElement.GetProperty("name").GetString());

        var update = await fixture.Client.PutAsJsonAsync($"/api/ticket-types/{typeId}", new
        {
            name = fixture.Name("Updated"), ticketCategory = "VE_THANG", price = 150000,
            durationDays = 30
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var locked = await fixture.Client.PutAsync($"/api/ticket-types/{typeId}/toggle-lock", null);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        await using (var db = fixture.CreateContext())
            Assert.False(await db.TicketTypes.Where(type => type.Id == typeId)
                .Select(type => type.IsActive).SingleAsync());

        var deleted = await fixture.Client.DeleteAsync($"/api/ticket-types/{typeId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        await using (var db = fixture.CreateContext())
        {
            var saved = await db.TicketTypes.AsNoTracking().SingleAsync(type => type.Id == typeId);
            Assert.True(saved.IsDeleted);
            Assert.False(saved.IsActive);
        }
    }

    [Fact]
    [Trait("TestCaseId", "TM-TT-02")]
    public async Task TicketTypeCreationRejectsInvalidDataAndUnauthorizedRoles()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PostAsJsonAsync("/api/ticket-types", new { name = "x" })).StatusCode);

        fixture.Use("SALES");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.PostAsJsonAsync("/api/ticket-types", new
            {
                name = fixture.Name("NoPermission"), ticketCategory = "VE_LUOT", price = 10000
            })).StatusCode);

        fixture.Use("ADMIN");
        var invalidName = fixture.Name("InvalidCategory");
        var invalid = await fixture.Client.PostAsJsonAsync("/api/ticket-types", new
        {
            name = invalidName, ticketCategory = "NOT_A_CATEGORY", price = 10000
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using var db = fixture.CreateContext();
        Assert.False(await db.TicketTypes.AnyAsync(type => type.Name == invalidName));
    }

    [Fact]
    [Trait("TestCaseId", "TM-TT-03")]
    public async Task TicketTypeListCanFindCreatedTypeAndDetailReturnsNotFoundForMissingType()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 10000, totalEntries: 1);

        var list = await fixture.Client.GetAsync("/api/ticket-types?pageIndex=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var body = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            Assert.Contains(body.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == typeId);

        var missing = await fixture.Client.GetAsync($"/api/ticket-types/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("TestCaseId", "TM-TT-04")]
    public async Task AdminCanReactivateLockedTicketType()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 10000, totalEntries: 1);
        fixture.Use("ADMIN");

        Assert.Equal(HttpStatusCode.OK,
            (await fixture.Client.PutAsync($"/api/ticket-types/{typeId}/toggle-lock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await fixture.Client.PutAsync($"/api/ticket-types/{typeId}/toggle-lock", null)).StatusCode);

        await using var db = fixture.CreateContext();
        Assert.True(await db.TicketTypes.Where(type => type.Id == typeId)
            .Select(type => type.IsActive).SingleAsync());
    }

    [Fact]
    [Trait("TestCaseId", "TM-VC-01")]
    public async Task VoucherListReturnsCreatedVoucherAndSupportsSearch()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");
        var code = fixture.VoucherCode();
        var created = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code, discountType = "PERCENTAGE", discountValue = 10, minOrderValue = 10000
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var voucherId = createdBody.RootElement.GetProperty("id").GetGuid();
        fixture.TrackVoucher(voucherId);

        var list = await fixture.Client.GetAsync($"/api/vouchers?searchTerm={Uri.EscapeDataString(code)}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listBody = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(listBody.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == voucherId && item.GetProperty("code").GetString() == code);
    }

    [Fact]
    [Trait("TestCaseId", "TM-VC-02")]
    public async Task AdminCanCreateVoucherAndPersistItsDiscountRules()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");
        var code = fixture.VoucherCode();

        var response = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code, discountType = "FIXED_AMOUNT", discountValue = 25000, minOrderValue = 100000,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var voucherId = body.RootElement.GetProperty("id").GetGuid();
        fixture.TrackVoucher(voucherId);

        await using var db = fixture.CreateContext();
        var saved = await db.Vouchers.AsNoTracking().SingleAsync(voucher => voucher.Id == voucherId);
        Assert.Equal(code, saved.Code);
        Assert.Equal("FIXED_AMOUNT", saved.DiscountType);
        Assert.Equal(25000m, saved.DiscountValue);
        Assert.Equal(100000m, saved.MinOrderValue);
        Assert.True(saved.IsActive);
    }

    [Fact]
    [Trait("TestCaseId", "TM-VC-03")]
    public async Task VoucherCreationRejectsUnauthorizedInvalidAndDuplicateRequests()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new
        {
            code = fixture.VoucherCode(), discountType = "PERCENTAGE", discountValue = 10
        };
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.PostAsJsonAsync("/api/vouchers", request)).StatusCode);

        fixture.Use("SALES");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.PostAsJsonAsync("/api/vouchers", request)).StatusCode);

        fixture.Use("ADMIN");
        var invalid = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code = fixture.VoucherCode(), discountType = "UNKNOWN", discountValue = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await fixture.Client.PostAsJsonAsync("/api/vouchers", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        fixture.TrackVoucher(createdBody.RootElement.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Conflict,
            (await fixture.Client.PostAsJsonAsync("/api/vouchers", request)).StatusCode);
    }

    [Fact]
    [Trait("TestCaseId", "TM-VC-04")]
    public async Task AdminCanUpdateVoucherAndDuplicateCodesConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");
        var originalCode = fixture.VoucherCode();
        var create = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code = originalCode, discountType = "PERCENTAGE", discountValue = 10
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createBody = System.Text.Json.JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var voucherId = createBody.RootElement.GetProperty("id").GetGuid();
        fixture.TrackVoucher(voucherId);

        var otherVoucher = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code = fixture.VoucherCode(), discountType = "PERCENTAGE", discountValue = 5
        });
        Assert.Equal(HttpStatusCode.Created, otherVoucher.StatusCode);
        using var otherBody = System.Text.Json.JsonDocument.Parse(await otherVoucher.Content.ReadAsStringAsync());
        var otherVoucherId = otherBody.RootElement.GetProperty("id").GetGuid();
        fixture.TrackVoucher(otherVoucherId);

        var conflict = await fixture.Client.PutAsJsonAsync($"/api/vouchers/{voucherId}", new
        {
            code = otherBody.RootElement.GetProperty("code").GetString(),
            discountType = "FIXED_AMOUNT", discountValue = 5000
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var updatedCode = fixture.VoucherCode();
        var update = await fixture.Client.PutAsJsonAsync($"/api/vouchers/{voucherId}", new
        {
            code = updatedCode, discountType = "FIXED_AMOUNT", discountValue = 5000,
            minOrderValue = 20000, isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var missing = await fixture.Client.PutAsJsonAsync($"/api/vouchers/{Guid.NewGuid()}", new
        {
            code = fixture.VoucherCode(), discountType = "PERCENTAGE", discountValue = 10
        });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await using var db = fixture.CreateContext();
        var saved = await db.Vouchers.AsNoTracking().SingleAsync(voucher => voucher.Id == voucherId);
        Assert.Equal(updatedCode.ToUpperInvariant(), saved.Code);
        Assert.Equal("FIXED_AMOUNT", saved.DiscountType);
        Assert.Equal(5000m, saved.DiscountValue);
        Assert.Equal(20000m, saved.MinOrderValue);
    }

    [Fact]
    [Trait("TestCaseId", "TM-VC-05")]
    public async Task AdminCanDeactivateAndReactivateVoucherAndUnauthorizedRoleIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");
        var create = await fixture.Client.PostAsJsonAsync("/api/vouchers", new
        {
            code = fixture.VoucherCode(), discountType = "PERCENTAGE", discountValue = 10
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var voucherId = body.RootElement.GetProperty("id").GetGuid();
        fixture.TrackVoucher(voucherId);

        fixture.Use("SALES");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.PutAsync($"/api/vouchers/{voucherId}/toggle-lock", null)).StatusCode);

        fixture.Use("ADMIN");
        Assert.Equal(HttpStatusCode.OK,
            (await fixture.Client.PutAsync($"/api/vouchers/{voucherId}/toggle-lock", null)).StatusCode);
        await using (var db = fixture.CreateContext())
            Assert.False(await db.Vouchers.Where(voucher => voucher.Id == voucherId)
                .Select(voucher => voucher.IsActive).SingleAsync());

        Assert.Equal(HttpStatusCode.OK,
            (await fixture.Client.PutAsync($"/api/vouchers/{voucherId}/toggle-lock", null)).StatusCode);
        await using (var db = fixture.CreateContext())
            Assert.True(await db.Vouchers.Where(voucher => voucher.Id == voucherId)
                .Select(voucher => voucher.IsActive).SingleAsync());
    }

    [Fact]
    [Trait("TestCaseId", "TM-OF-01")]
    public async Task SalesCanSellMultiEntryTicketAtCounterAndPersistOrderPaymentAndTicket()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 10000, totalEntries: 1);
        fixture.Use("SALES");

        var response = await fixture.Client.PostAsJsonAsync("/api/tickets/sell-offline", new
        {
            items = new[] { new { ticketTypeId = typeId, quantity = 2, requestedEntries = 3 } },
            customerName = "Test Walk-in", customerPhone = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var orderId = body.RootElement.GetProperty("orderId").GetGuid();
        fixture.TrackOrder(orderId);
        Assert.Equal(60000m, body.RootElement.GetProperty("finalAmount").GetDecimal());
        Assert.Equal(2, body.RootElement.GetProperty("tickets").GetArrayLength());
        foreach (var ticket in body.RootElement.GetProperty("tickets").EnumerateArray())
            fixture.TrackTicket(ticket.GetProperty("id").GetGuid());

        await using var db = fixture.CreateContext();
        var order = await db.Orders.AsNoTracking().SingleAsync(item => item.Id == orderId);
        Assert.Equal("COMPLETED", order.Status);
        Assert.Single(await db.OrderDetails.Where(item => item.OrderId == orderId).ToListAsync());
        var payment = await db.Payments.AsNoTracking().SingleAsync(item => item.OrderId == orderId);
        Assert.Equal(60000m, payment.Amount);
        Assert.Equal("CASH", payment.PaymentMethod);
        Assert.Equal("COMPLETED", payment.Status);
        Assert.Equal(2, await db.Tickets.CountAsync(item => item.TicketTypeId == typeId && item.Status == "ACTIVE"));
    }

    [Fact]
    [Trait("TestCaseId", "TM-ON-01")]
    public async Task CustomerCheckoutRemainsPendingUntilWebhookPaysAndIssuesOwnedTicket()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 12500, totalEntries: 1);
        fixture.Use("CUSTOMER");

        var checkout = await fixture.Client.PostAsJsonAsync("/api/tickets/checkout", new
        {
            items = new[] { new { ticketTypeId = typeId, quantity = 1, requestedEntries = 2 } }
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        using var checkoutBody = System.Text.Json.JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        var orderId = checkoutBody.RootElement.GetProperty("orderId").GetGuid();
        fixture.TrackOrder(orderId);
        var transactionRef = checkoutBody.RootElement.GetProperty("transactionRef").GetString()!;
        Assert.Equal("PENDING", checkoutBody.RootElement.GetProperty("status").GetString());

        using (var beforePayment = await fixture.Client.GetAsync($"/api/tickets/me/orders/{orderId}/status"))
        {
            Assert.Equal(HttpStatusCode.OK, beforePayment.StatusCode);
            using var statusBody = System.Text.Json.JsonDocument.Parse(await beforePayment.Content.ReadAsStringAsync());
            Assert.Equal("PENDING", statusBody.RootElement.GetProperty("status").GetString());
        }

        var webhook = await fixture.Client.PostAsJsonAsync("/api/sepay/webhook", new
        {
            content = $"Payment {transactionRef}", transferAmount = 25000, referenceCode = fixture.Name("BANK")
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        await using var db = fixture.CreateContext();
        Assert.Equal("COMPLETED", await db.Orders.Where(item => item.Id == orderId)
            .Select(item => item.Status).SingleAsync());
        var ticket = await db.Tickets.AsNoTracking().SingleAsync(item => item.UserId == fixture.UserId("CUSTOMER")
            && item.TicketTypeId == typeId);
        Assert.Equal(2, ticket.RemainingEntries);
        Assert.Equal("COMPLETED", await db.Payments.Where(item => item.OrderId == orderId
            && item.TransactionRef == transactionRef).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    [Trait("TestCaseId", "TM-TK-01")]
    public async Task CustomerCanReadAndUpdateOwnTicketCodeButCannotChangeAnotherCustomersTicket()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 10000, totalEntries: 1);
        var ticketId = await fixture.AddTicketAsync(typeId, fixture.UserId("CUSTOMER"), "QA100001");
        fixture.Use("CUSTOMER");

        var tickets = await fixture.Client.GetAsync("/api/tickets/me");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
        using (var ticketList = System.Text.Json.JsonDocument.Parse(await tickets.Content.ReadAsStringAsync()))
            Assert.Contains(ticketList.RootElement.EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == ticketId && item.GetProperty("qrCode").GetString() == "QA100001");

        var updated = await fixture.Client.PutAsJsonAsync($"/api/tickets/{ticketId}/code", new { code = "123456" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("123456", await fixture.ReadTicketCodeAsync(ticketId));

        fixture.Use("OTHER_CUSTOMER");
        var forbidden = await fixture.Client.PutAsJsonAsync($"/api/tickets/{ticketId}/code", new { code = "654321" });
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
        Assert.Equal("123456", await fixture.ReadTicketCodeAsync(ticketId));
    }

    [Fact]
    [Trait("TestCaseId", "TM-TK-02")]
    public async Task LockedTicketTypeCannotBeSoldAndCreatesNoOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var typeId = await fixture.AddTicketTypeAsync("VE_LUOT", price: 10000, totalEntries: 1);
        fixture.Use("ADMIN");
        Assert.Equal(HttpStatusCode.OK,
            (await fixture.Client.PutAsync($"/api/ticket-types/{typeId}/toggle-lock", null)).StatusCode);
        fixture.Use("CUSTOMER");

        await using var db = fixture.CreateContext();
        var ordersBefore = await db.Orders.CountAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/tickets/checkout", new
        {
            items = new[] { new { ticketTypeId = typeId, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ordersBefore, await db.Orders.CountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SmartPoolDbContext> options;
        private readonly WebApplication app;
        private readonly Dictionary<string, User> users;
        private readonly HashSet<Guid> ticketTypeIds = [];
        private readonly HashSet<Guid> ticketIds = [];
        private readonly HashSet<Guid> orderIds = [];
        private readonly HashSet<Guid> voucherIds = [];
        private readonly HashSet<Guid> userIds;
        private readonly HashSet<Guid> roleIds;

        private Fixture(DbContextOptions<SmartPoolDbContext> options, WebApplication app,
            Dictionary<string, User> users, HashSet<Guid> userIds, HashSet<Guid> roleIds)
        {
            this.options = options;
            this.app = app;
            this.users = users;
            this.userIds = userIds;
            this.roleIds = roleIds;
            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var scope = app.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
            Tokens = users.ToDictionary(item => item.Key,
                item => tokens.CreateToken(item.Value, item.Value.Role!.Name).Token);
        }

        public HttpClient Client { get; }
        private Dictionary<string, string> Tokens { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var db = new SmartPoolDbContext(options);
            var suffix = Guid.NewGuid().ToString("N");
            var roles = new Dictionary<string, Role>();
            var permissions = new Dictionary<string, string>
            {
                ["ADMIN"] = """{"ticket_types":["read","create","update","delete"],"ticket_sales":["read","create"],"vouchers":["read","create","update","delete"]}""",
                ["SALES"] = """{"ticket_types":["read"],"ticket_sales":["read","create"]}""",
                ["CUSTOMER"] = """{"ticket_types":["read"],"customer_tickets":["read","create","update"]}""",
                ["OTHER_CUSTOMER"] = """{"customer_tickets":["read","create","update"]}"""
            };
            foreach (var (key, value) in permissions)
            {
                var role = new Role
                {
                    Id = Guid.NewGuid(), Name = $"QA_TICKET_{key}_{suffix}", Permissions = value, IsDeleted = false
                };
                roles[key] = role;
                db.Roles.Add(role);
            }

            var users = roles.ToDictionary(item => item.Key, item => new User
            {
                Id = Guid.NewGuid(), Role = item.Value, Username = $"qa_ticket_{item.Key}_{suffix}",
                Email = $"{item.Key.ToLowerInvariant()}_{suffix}@example.test", PasswordHash = "test-only",
                Status = "ACTIVE", IsDeleted = false
            });
            db.Users.AddRange(users.Values);
            await db.SaveChangesAsync();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
                ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
                ["JwtSettings:SecretKey"] = "ticket-management-integration-tests-signing-key",
                ["JwtSettings:Issuer"] = "ticket-management-tests",
                ["JwtSettings:Audience"] = "ticket-management-tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
                ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
                ["CloudinarySettings:CloudName"] = "test"
            });
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddApiAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(TicketController).Assembly);
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            try
            {
                await app.StartAsync();
                return new Fixture(options, app, users, users.Values.Select(user => user.Id).ToHashSet(),
                    roles.Values.Select(role => role.Id).ToHashSet());
            }
            catch
            {
                await app.DisposeAsync();
                await using var cleanup = new SmartPoolDbContext(options);
                cleanup.Users.RemoveRange(await cleanup.Users.Where(user => users.Values.Select(item => item.Id).Contains(user.Id)).ToListAsync());
                cleanup.Roles.RemoveRange(await cleanup.Roles.Where(role => roles.Values.Select(item => item.Id).Contains(role.Id)).ToListAsync());
                await cleanup.SaveChangesAsync();
                throw;
            }
        }

        public SmartPoolDbContext CreateContext() => new(options);
        public Guid UserId(string key) => users[key].Id;
        public string Name(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
        public string VoucherCode() => $"QA_{Guid.NewGuid():N}"[..23];
        public void TrackTicketType(Guid id) => ticketTypeIds.Add(id);
        public void TrackTicket(Guid id) => ticketIds.Add(id);
        public void TrackOrder(Guid id) => orderIds.Add(id);
        public void TrackVoucher(Guid id) => voucherIds.Add(id);

        public void Use(string key)
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Tokens[key]);
        }

        public async Task<Guid> AddTicketTypeAsync(string category, decimal price, int totalEntries)
        {
            var id = Guid.NewGuid();
            ticketTypeIds.Add(id);
            await using var db = CreateContext();
            db.TicketTypes.Add(new TicketType
            {
                Id = id, Name = Name("QA Ticket"), TicketCategory = category, Price = price,
                TotalEntries = totalEntries, DurationDays = category == "VE_THANG" ? 30 : null,
                IsActive = true, IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> AddTicketAsync(Guid typeId, Guid userId, string code)
        {
            var id = Guid.NewGuid();
            ticketIds.Add(id);
            await using var db = CreateContext();
            db.Tickets.Add(new Ticket
            {
                Id = id, TicketTypeId = typeId, UserId = userId, QrCode = code,
                Status = "ACTIVE", IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<string?> ReadTicketCodeAsync(Guid id)
        {
            await using var db = CreateContext();
            return await db.Tickets.Where(ticket => ticket.Id == id).Select(ticket => ticket.QrCode).SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await using var db = CreateContext();
            var allOrderIds = orderIds.Concat(await db.Orders
                .Where(order => order.UserId != null && userIds.Contains(order.UserId.Value))
                .Select(order => order.Id).ToListAsync()).Distinct().ToArray();
            await db.EntryLogs.Where(log => log.TicketId != null && ticketIds.Contains(log.TicketId.Value)).ExecuteDeleteAsync();
            await db.Tickets.Where(ticket => ticketIds.Contains(ticket.Id) || userIds.Contains(ticket.UserId ?? Guid.Empty)).ExecuteDeleteAsync();
            await db.Payments.Where(payment => allOrderIds.Contains(payment.OrderId)).ExecuteDeleteAsync();
            await db.OrderDetails.Where(detail => allOrderIds.Contains(detail.OrderId)).ExecuteDeleteAsync();
            await db.Orders.Where(order => allOrderIds.Contains(order.Id)).ExecuteDeleteAsync();
            await db.Vouchers.Where(voucher => voucherIds.Contains(voucher.Id)).ExecuteDeleteAsync();
            await db.TicketTypes.Where(type => ticketTypeIds.Contains(type.Id)).ExecuteDeleteAsync();
            await db.UserProfiles.Where(profile => userIds.Contains(profile.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
            await db.Roles.Where(role => roleIds.Contains(role.Id)).ExecuteDeleteAsync();
        }
    }
}
