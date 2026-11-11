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
using SmartPool.Application.Interfaces;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.IntegrationTests.ManagePayments;

public sealed class PaymentManagementApiTests
{
    [Fact]
    [Trait("TestCaseId", "PM-RN-01")]
    public async Task AdminCanReadIssuedRentalPaymentInPaymentManagement()
    {
        await using var fixture = await Fixture.CreateAsync();
        var productId = await fixture.AddProductAsync();
        var requestId = Guid.NewGuid();
        fixture.Use("ADMIN");

        var qr = await fixture.Client.PostAsJsonAsync("/api/rentals/payments/qr", new
        {
            requestId,
            customerName = "QA Rental Customer",
            customerPhone = "0900000002",
            items = new[] { new { productId, quantity = 1, expectedUnitPrice = 10000 } }
        });
        Assert.Equal(HttpStatusCode.Created, qr.StatusCode);
        using var qrBody = System.Text.Json.JsonDocument.Parse(await qr.Content.ReadAsStringAsync());
        var intentId = qrBody.RootElement.GetProperty("id").GetGuid();
        var orderId = qrBody.RootElement.GetProperty("orderId").GetGuid();
        var transactionRef = qrBody.RootElement.GetProperty("transactionRef").GetString()!;
        fixture.TrackRentalIntent(intentId);
        fixture.TrackOrder(orderId);

        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sepay/rental/webhook")
        {
            Content = JsonContent.Create(new
            {
                id = fixture.NextBankEventId(), transferType = "in", accountNumber = "test-account",
                transferAmount = qrBody.RootElement.GetProperty("totalToCollect").GetDecimal(),
                content = transactionRef, referenceCode = fixture.Name("QA_RENTAL_BANK")
            })
        };
        webhookRequest.Headers.Authorization = new AuthenticationHeaderValue("Apikey", "test-only");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(webhookRequest)).StatusCode);

        fixture.Use("ADMIN");
        var issue = await fixture.Client.PostAsync($"/api/rentals/payments/{intentId}/issue", null);
        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);

        var paymentId = await fixture.PaymentIdForOrderAsync(orderId);
        var detail = await fixture.Client.GetAsync($"/api/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailBody = System.Text.Json.JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var payment = detailBody.RootElement;
        Assert.Equal("COMPLETED", payment.GetProperty("status").GetString());
        Assert.Equal("TRANSFER", payment.GetProperty("paymentMethod").GetString());
        Assert.Equal(10000m, payment.GetProperty("amount").GetDecimal());
        Assert.Equal("QA Rental Customer", payment.GetProperty("customerName").GetString());
        Assert.Equal("Renting", payment.GetProperty("orderStatus").GetString());
        var item = Assert.Single(payment.GetProperty("items").EnumerateArray());
        Assert.Equal("QA Rental Product", item.GetProperty("itemName").GetString());
        Assert.Equal(10000m, item.GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    [Trait("TestCaseId", "PM-LS-01")]
    public async Task AdminCanFindAndReadCompletedCounterPayment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticketTypeId = await fixture.AddTicketTypeAsync();
        fixture.Use("SALES");
        var customerName = fixture.Name("QA Counter");

        var sale = await fixture.Client.PostAsJsonAsync("/api/tickets/sell-offline", new
        {
            items = new[] { new { ticketTypeId, quantity = 2, requestedEntries = 3 } },
            customerName, customerPhone = "0900000000"
        });
        Assert.Equal(HttpStatusCode.OK, sale.StatusCode);
        using var saleBody = System.Text.Json.JsonDocument.Parse(await sale.Content.ReadAsStringAsync());
        var orderId = saleBody.RootElement.GetProperty("orderId").GetGuid();
        fixture.TrackOrder(orderId);
        foreach (var ticket in saleBody.RootElement.GetProperty("tickets").EnumerateArray())
            fixture.TrackTicket(ticket.GetProperty("id").GetGuid());

        var paymentId = await fixture.PaymentIdForOrderAsync(orderId);
        fixture.Use("ADMIN");
        var list = await fixture.Client.GetAsync($"/api/payments?status=COMPLETED&paymentMethod=cash&search={Uri.EscapeDataString(customerName)}&pageIndex=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var listBody = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var root = listBody.RootElement;
            Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, root.GetProperty("pageIndex").GetInt32());
            Assert.Equal(5, root.GetProperty("pageSize").GetInt32());
            var summary = Assert.Single(root.GetProperty("items").EnumerateArray());
            Assert.Equal(paymentId, summary.GetProperty("paymentId").GetGuid());
            Assert.Equal(customerName, summary.GetProperty("customerName").GetString());
            Assert.Equal(60000m, summary.GetProperty("amount").GetDecimal());
            Assert.Equal("CASH", summary.GetProperty("paymentMethod").GetString());
            Assert.Equal("COMPLETED", summary.GetProperty("status").GetString());
            Assert.Contains("QA Payment Ticket", summary.GetProperty("itemsSummary").GetString());
        }

        var detail = await fixture.Client.GetAsync($"/api/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailBody = System.Text.Json.JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var payment = detailBody.RootElement;
        Assert.Equal(orderId, payment.GetProperty("orderId").GetGuid());
        Assert.Equal(customerName, payment.GetProperty("customerName").GetString());
        Assert.Equal("COMPLETED", payment.GetProperty("orderStatus").GetString());
        var item = Assert.Single(payment.GetProperty("items").EnumerateArray());
        Assert.Equal("QA Payment Ticket", item.GetProperty("itemName").GetString());
        Assert.Equal("VE_LUOT", item.GetProperty("ticketCategory").GetString());
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());
        Assert.Equal(30000m, item.GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    [Trait("TestCaseId", "PM-ON-01")]
    public async Task AdminCanSeeOnlinePaymentPendingThenCompletedAfterWebhook()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticketTypeId = await fixture.AddTicketTypeAsync(price: 12500);
        fixture.Use("CUSTOMER");

        var checkout = await fixture.Client.PostAsJsonAsync("/api/tickets/checkout", new
        {
            items = new[] { new { ticketTypeId, quantity = 1, requestedEntries = 2 } }
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        using var checkoutBody = System.Text.Json.JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        var orderId = checkoutBody.RootElement.GetProperty("orderId").GetGuid();
        fixture.TrackOrder(orderId);
        var transactionRef = checkoutBody.RootElement.GetProperty("transactionRef").GetString()!;
        Assert.Equal("PENDING", checkoutBody.RootElement.GetProperty("status").GetString());
        var paymentId = await fixture.PaymentIdForOrderAsync(orderId);

        fixture.Use("ADMIN");
        var pending = await fixture.Client.GetAsync($"/api/payments?status=PENDING&search={Uri.EscapeDataString("QA Online Customer")}");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using (var pendingBody = System.Text.Json.JsonDocument.Parse(await pending.Content.ReadAsStringAsync()))
            Assert.Contains(pendingBody.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("paymentId").GetGuid() == paymentId
                && item.GetProperty("status").GetString() == "PENDING");

        var webhook = await fixture.Client.PostAsJsonAsync("/api/sepay/webhook", new
        {
            content = $"Payment {transactionRef}", transferAmount = 25000,
            referenceCode = fixture.Name("QA_BANK")
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var detail = await fixture.Client.GetAsync($"/api/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailBody = System.Text.Json.JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var payment = detailBody.RootElement;
        Assert.Equal("COMPLETED", payment.GetProperty("status").GetString());
        Assert.Equal("COMPLETED", payment.GetProperty("orderStatus").GetString());
        Assert.Equal("QA Online Customer", payment.GetProperty("customerName").GetString());
        Assert.Equal(25000m, payment.GetProperty("amount").GetDecimal());
        var ticket = Assert.Single(payment.GetProperty("tickets").EnumerateArray());
        Assert.Equal("QA Payment Ticket", ticket.GetProperty("ticketName").GetString());
        Assert.Equal(2, ticket.GetProperty("remainingEntries").GetInt32());
    }

    [Fact]
    [Trait("TestCaseId", "PM-AU-01")]
    public async Task PaymentReadEndpointsRequirePaymentsReadPermission()
    {
        await using var fixture = await Fixture.CreateAsync();
        var missingId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.GetAsync("/api/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client.GetAsync($"/api/payments/{missingId}")).StatusCode);

        fixture.Use("SALES");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.GetAsync("/api/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.Client.GetAsync($"/api/payments/{missingId}")).StatusCode);
    }

    [Fact]
    [Trait("TestCaseId", "PM-NF-01")]
    public async Task AdminGetsNotFoundForMissingPayment()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Use("ADMIN");

        var response = await fixture.Client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DbContextOptions<SmartPoolDbContext> options;
        private readonly WebApplication app;
        private readonly Dictionary<string, User> users;
        private readonly HashSet<Guid> ticketTypeIds = [];
        private readonly HashSet<Guid> productIds = [];
        private readonly HashSet<Guid> ticketIds = [];
        private readonly HashSet<Guid> orderIds = [];
        private readonly HashSet<Guid> rentalIntentIds = [];
        private readonly HashSet<Guid> userIds;
        private readonly HashSet<Guid> roleIds;
        private static long nextBankEventId = DateTime.UtcNow.Ticks;

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
            var permissions = new Dictionary<string, string>
            {
                ["ADMIN"] = """{"payments":["read"],"ticket_types":["read","create","update","delete"],"ticket_sales":["read","create"],"rentals":["read","create","update"]}""",
                ["SALES"] = """{"ticket_sales":["read","create"]}""",
                ["CUSTOMER"] = """{"ticket_types":["read"],"customer_tickets":["read","create","update"]}"""
            };
            var roles = permissions.ToDictionary(item => item.Key, item => new Role
            {
                Id = Guid.NewGuid(), Name = $"QA_PAYMENT_{item.Key}_{suffix}",
                Permissions = item.Value, IsDeleted = false
            });
            db.Roles.AddRange(roles.Values);
            var users = roles.ToDictionary(item => item.Key, item => new User
            {
                Id = Guid.NewGuid(), Role = item.Value, Username = $"qa_payment_{item.Key}_{suffix}",
                Email = $"{item.Key.ToLowerInvariant()}_{suffix}@example.test", PasswordHash = "test-only",
                Phone = item.Key == "CUSTOMER" ? "0900000001" : null,
                Status = "ACTIVE", IsDeleted = false
            });
            db.Users.AddRange(users.Values);
            db.UserProfiles.Add(new UserProfile
            {
                UserId = users["CUSTOMER"].Id, FullName = "QA Online Customer", Address = "Test address"
            });
            await db.SaveChangesAsync();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = IntegrationTestDatabase.GetConnectionString(),
                ["ConnectionStrings:Redis"] = IntegrationTestSettings.RedisConnection,
                ["JwtSettings:SecretKey"] = "payment-management-integration-tests-signing-key",
                ["JwtSettings:Issuer"] = "payment-management-tests",
                ["JwtSettings:Audience"] = "payment-management-tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "5",
                ["SePay:ApiKey"] = "test-only", ["SePay:AccountNumber"] = "test-account",
                ["SePay:BankId"] = "test-bank", ["SePay:AccountName"] = "Test",
                ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test",
                ["CloudinarySettings:CloudName"] = "test"
            });
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddApiAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly);
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
                cleanup.UserProfiles.RemoveRange(await cleanup.UserProfiles
                    .Where(profile => users.Values.Select(item => item.Id).Contains(profile.UserId)).ToListAsync());
                cleanup.Users.RemoveRange(await cleanup.Users
                    .Where(user => users.Values.Select(item => item.Id).Contains(user.Id)).ToListAsync());
                cleanup.Roles.RemoveRange(await cleanup.Roles
                    .Where(role => roles.Values.Select(item => item.Id).Contains(role.Id)).ToListAsync());
                await cleanup.SaveChangesAsync();
                throw;
            }
        }

        public void Use(string key) => Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Tokens[key]);

        public string Name(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
        public void TrackOrder(Guid id) => orderIds.Add(id);
        public void TrackTicket(Guid id) => ticketIds.Add(id);
        public void TrackRentalIntent(Guid id) => rentalIntentIds.Add(id);
        public long NextBankEventId() => Interlocked.Increment(ref nextBankEventId);

        public async Task<Guid> AddProductAsync()
        {
            var id = Guid.NewGuid();
            productIds.Add(id);
            await using var db = new SmartPoolDbContext(options);
            db.Products.Add(new Product
            {
                Id = id, Name = "QA Rental Product", Price = 10000, StockQuantity = 5,
                IsActive = true, IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> AddTicketTypeAsync(decimal price = 10000)
        {
            var id = Guid.NewGuid();
            ticketTypeIds.Add(id);
            await using var db = new SmartPoolDbContext(options);
            db.TicketTypes.Add(new TicketType
            {
                Id = id, Name = "QA Payment Ticket", TicketCategory = "VE_LUOT", Price = price,
                TotalEntries = 1, IsActive = true, IsDeleted = false
            });
            await db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> PaymentIdForOrderAsync(Guid orderId)
        {
            await using var db = new SmartPoolDbContext(options);
            return await db.Payments.Where(payment => payment.OrderId == orderId)
                .Select(payment => payment.Id).SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await using var db = new SmartPoolDbContext(options);
            var allOrderIds = orderIds.Concat(await db.Orders
                .Where(order => order.UserId != null && userIds.Contains(order.UserId.Value))
                .Select(order => order.Id).ToListAsync()).Distinct().ToArray();
            await db.BankTransferReceipts.Where(receipt => receipt.OrderId.HasValue
                && allOrderIds.Contains(receipt.OrderId.Value)).ExecuteDeleteAsync();
            await db.RentalPaymentIntents.Where(intent => rentalIntentIds.Contains(intent.Id)).ExecuteDeleteAsync();
            await db.EntryLogs.Where(log => log.TicketId != null && ticketIds.Contains(log.TicketId.Value))
                .ExecuteDeleteAsync();
            await db.RentalTransactions.Where(item => allOrderIds.Contains(item.OrderId)).ExecuteDeleteAsync();
            await db.InventoryLogs.Where(item => productIds.Contains(item.ProductId)).ExecuteDeleteAsync();
            await db.Rentals.Where(item => productIds.Contains(item.ProductId)).ExecuteDeleteAsync();
            await db.Tickets.Where(ticket => ticketIds.Contains(ticket.Id)
                || userIds.Contains(ticket.UserId ?? Guid.Empty)).ExecuteDeleteAsync();
            await db.Payments.Where(payment => allOrderIds.Contains(payment.OrderId)).ExecuteDeleteAsync();
            await db.OrderDetails.Where(detail => allOrderIds.Contains(detail.OrderId)).ExecuteDeleteAsync();
            await db.Orders.Where(order => allOrderIds.Contains(order.Id)).ExecuteDeleteAsync();
            await db.TicketTypes.Where(type => ticketTypeIds.Contains(type.Id)).ExecuteDeleteAsync();
            await db.Products.Where(product => productIds.Contains(product.Id)).ExecuteDeleteAsync();
            await db.UserProfiles.Where(profile => userIds.Contains(profile.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
            await db.Roles.Where(role => roleIds.Contains(role.Id)).ExecuteDeleteAsync();
        }
    }
}
