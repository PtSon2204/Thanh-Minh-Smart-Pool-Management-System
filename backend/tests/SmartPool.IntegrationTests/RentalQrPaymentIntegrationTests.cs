using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Services;

namespace SmartPool.IntegrationTests;

public sealed class RentalQrPaymentIntegrationTests
{
    [Fact]
    public async Task GivenDuplicateRentalEvent_WhenApplyingConcurrently_ThenItCreditsOneReceiptAndFlagsChangedPayload()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var rentalOperations = fixture.Operations(db);
        var intent = await rentalOperations.CreateQrAsync(fixture.QrRequest(1), default);
        var transfer = fixture.Transfer(intent.TransactionRef, 30000);
        await using var firstEventDb = fixture.Context();
        await using var secondEventDb = fixture.Context();
        var firstOperations = fixture.Operations(firstEventDb);
        var secondOperations = fixture.Operations(secondEventDb);

        var concurrentResults = await Task.WhenAll(firstOperations.ApplyTransferAsync(transfer, default), secondOperations.ApplyTransferAsync(transfer, default));
        var replay = await firstOperations.ApplyTransferAsync(transfer, default);
        var collision = await secondOperations.ApplyTransferAsync(transfer with { Amount = 31000 }, default);

        Assert.All(concurrentResults, result => Assert.True(result.Success));
        Assert.True(replay.Success);
        Assert.True(collision.Success);
        var status = await rentalOperations.GetPaymentAsync(intent.Id, default);
        Assert.Equal(RentalPaymentPolicy.Paid, status.Status);
        Assert.Equal(30000m, status.PaidAmount);
        Assert.True(status.RequiresReconciliation);
        Assert.Equal(1, await db.BankTransferReceipts.CountAsync(item => item.OrderId == intent.OrderId && item.Disposition == "RENTAL_APPLIED"));
        Assert.False(await db.Rentals.AnyAsync(item => item.OrderId == intent.OrderId));
    }

    [Fact]
    public async Task GivenVerifiedOverpayment_WhenIssuingAndReturningNormally_ThenFeeStaysRevenueAndRefundIsRecordedAsCash()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.QrRequest(2);
        RentalPaymentStatusResponse intent;
        await using (var db = fixture.Context())
        {
            intent = await fixture.Operations(db).CreateQrAsync(request, default);
            Assert.Equal(RentalPaymentPolicy.Pending, intent.Status);
            Assert.Equal((20000m, 40000m, 60000m), (intent.FeeAmount, intent.DepositAmount, intent.TotalToCollect));
        }
        Assert.Equal(3, await fixture.ReadStockAsync());
        await using (var db = fixture.Context())
        {
            Assert.False(await db.Rentals.AnyAsync(item => item.OrderId == intent.OrderId));
            Assert.False(await db.Payments.AnyAsync(item => item.OrderId == intent.OrderId));
            Assert.False(await db.RentalTransactions.AnyAsync(item => item.OrderId == intent.OrderId));
        }

        await using (var db = fixture.Context())
        {
            var result = await fixture.Operations(db).ApplyTransferAsync(
                fixture.Transfer(intent.TransactionRef, 10000), default);
            Assert.True(result.Success);
            var partial = await fixture.Operations(db).GetPaymentAsync(intent.Id, default);
            Assert.Equal(RentalPaymentPolicy.Partial, partial.Status);
            Assert.Equal(50000m, partial.RemainingAmount);
            Assert.False(await db.Rentals.AnyAsync(item => item.OrderId == intent.OrderId));
        }
        await using (var db = fixture.Context())
        {
            var result = await fixture.Operations(db).ApplyTransferAsync(
                fixture.Transfer(intent.TransactionRef, 55000), default);
            Assert.True(result.Success);
        }
        await using var issueDb = fixture.Context();
        await using var concurrentIssueDb = fixture.Context();
        var operations = fixture.Operations(issueDb);
        var concurrentIssueOperations = fixture.Operations(concurrentIssueDb);
        var paid = await operations.GetPaymentAsync(intent.Id, default);
        Assert.Equal(RentalPaymentPolicy.Paid, paid.Status);
        Assert.Equal((65000m, 0m, 5000m, true),
            (paid.PaidAmount, paid.RemainingAmount, paid.ExcessAmount, paid.RequiresReconciliation));

        var issueResults = await Task.WhenAll(
            operations.IssuePaymentAsync(intent.Id, fixture.OperatorId, default),
            concurrentIssueOperations.IssuePaymentAsync(intent.Id, fixture.OperatorId, default));
        var issued = issueResults[0];
        var replay = issueResults[1];
        Assert.Equal(issued.Rentals.Select(item => item.Id), replay.Rentals.Select(item => item.Id));
        Assert.Equal(3, await fixture.ReadStockAsync());
        Assert.Equal(2, await issueDb.Rentals.CountAsync(item => item.OrderId == intent.OrderId));
        var payment = await issueDb.Payments.Where(item => item.OrderId == intent.OrderId)
            .Select(item => new { item.Amount, item.PaymentMethod }).SingleAsync();
        Assert.Equal(20000m, payment.Amount);
        Assert.Equal("TRANSFER", payment.PaymentMethod);
        Assert.Equal((20000m, 40000m), await issueDb.RentalTransactions.Where(item => item.OrderId == intent.OrderId && item.Kind == "Collection")
            .Select(item => new ValueTuple<decimal, decimal>(item.FeeAmount, item.DepositAmount)).SingleAsync());
        var returnRequest = new ReturnRentalQuantityCommand
        {
            RequestId = Guid.NewGuid(), OrderId = intent.OrderId, ProductId = fixture.ProductId,
            OperatorId = fixture.OperatorId, RentalIds = issued.Rentals.Select(item => item.Id).ToList(),
            SettlementConfirmed = true
        };
        var returned = await operations.ReturnRentalQuantityAsync(returnRequest, default);
        var replayedReturn = await operations.ReturnRentalQuantityAsync(returnRequest, default);
        Assert.Equal(returned.RefundAmount, replayedReturn.RefundAmount);
        Assert.Equal((40000m, 0m), (returned.RefundAmount, returned.RetainedAmount));
        var transactions = await issueDb.RentalTransactions.Where(item => item.OrderId == intent.OrderId).ToListAsync();
        var settlement = Assert.Single(transactions, item => item.Kind == "Settlement");
        Assert.Equal("CASH", settlement.RefundMethod);
        Assert.Equal(fixture.OperatorId, settlement.ConfirmedByUserId);
        Assert.Equal(5, await fixture.ReadStockAsync());
        Assert.Equal(1, await issueDb.Payments.CountAsync(item => item.OrderId == intent.OrderId));
    }

    [Fact]
    public async Task GivenPartialReceiptAtDeadline_WhenExpiredThenPaidLate_ThenStockStaysReleasedAndMoneyNeedsReconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        RentalPaymentStatusResponse intent;
        await using (var db = fixture.Context())
            intent = await fixture.Operations(db).CreateQrAsync(fixture.QrRequest(1), default);
        Assert.Equal(4, await fixture.ReadStockAsync());

        await using (var db = fixture.Context())
            Assert.True((await fixture.Operations(db).ApplyTransferAsync(fixture.Transfer(intent.TransactionRef, 10000), default)).Success);
        fixture.Clock.Advance(RentalPaymentPolicy.Lifetime);
        await using (var db = fixture.Context())
        {
            var expired = await fixture.Operations(db).GetPaymentAsync(intent.Id, default);
            Assert.Equal(RentalPaymentPolicy.Expired, expired.Status);
            Assert.Equal(5, await fixture.ReadStockAsync());
        }

        await using (var db = fixture.Context())
        {
            var late = await fixture.Operations(db).ApplyTransferAsync(fixture.Transfer(intent.TransactionRef, 50000), default);
            Assert.True(late.Success);
        }
        await using var finalDb = fixture.Context();
        var result = await fixture.Operations(finalDb).GetPaymentAsync(intent.Id, default);
        Assert.Equal(RentalPaymentPolicy.Expired, result.Status);
        Assert.Equal(60000m, result.PaidAmount);
        Assert.Equal(30000m, result.ExcessAmount);
        Assert.True(result.RequiresReconciliation);
        Assert.False(await finalDb.Rentals.AnyAsync(item => item.OrderId == intent.OrderId));
        Assert.False(await finalDb.RentalTransactions.AnyAsync(item => item.OrderId == intent.OrderId && item.Kind == "Collection"));
        Assert.Equal(2, await finalDb.BankTransferReceipts.CountAsync(item => item.OrderId == intent.OrderId));
        Assert.Equal(5, await fixture.ReadStockAsync());
    }

    private sealed class ManualClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = initial;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan duration) => Now = Now.Add(duration);
    }

    private sealed class Fixture(DbContextOptions<SmartPoolDbContext> options, Guid operatorId, ManualClock clock) : IAsyncDisposable
    {
        private static long nextEventId = DateTime.UtcNow.Ticks;
        private readonly DbContextOptions<SmartPoolDbContext> _options = options;
        public Guid OperatorId { get; } = operatorId;
        public Guid ProductId { get; } = Guid.NewGuid();
        public ManualClock Clock { get; } = clock;

        public SmartPoolDbContext Context() => new(_options);

        public ServiceOperations Operations(SmartPoolDbContext context) => new(context,
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper(), Clock,
            Options.Create(SePayOptions));

        private SePayOptions SePayOptions { get; } = new()
        {
            ApiKey = "test-only", AccountNumber = "test-account", BankId = "test-bank", AccountName = "Test"
        };

        public CreateRentalQrCommand QrRequest(int quantity) => new()
        {
            RequestId = Guid.NewGuid(), OperatorId = OperatorId, CustomerName = "QA QR customer", CustomerPhone = "0900000000",
            Items = [new() { ProductId = ProductId, Quantity = quantity, ExpectedUnitPrice = 10000 }]
        };

        public BankTransferEvent Transfer(string reference, decimal amount) => new(
            Interlocked.Increment(ref nextEventId), "test-account", amount, reference, $"BANK-{Guid.NewGuid():N}");

        public async Task<int> ReadStockAsync()
        {
            await using var context = Context();
            return await context.Products.Where(item => item.Id == ProductId).Select(item => item.StockQuantity!.Value).SingleAsync();
        }

        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var context = new SmartPoolDbContext(options);
            var actor = await context.Users.Select(item => item.Id).FirstAsync();
            var now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
            var fixture = new Fixture(options, actor, new ManualClock(now));
            context.Products.Add(new Product
            {
                Id = fixture.ProductId, Name = $"QA QR rental {fixture.ProductId:N}", Price = 10000,
                StockQuantity = 5, IsActive = true, IsDeleted = false, CreatedAt = now.UtcDateTime, UpdatedAt = now.UtcDateTime
            });
            await context.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = Context();
            var orderIds = await context.OrderDetails.Where(item => item.ItemId == ProductId && item.ItemType == "Product")
                .Select(item => item.OrderId).Distinct().ToListAsync();
            orderIds.AddRange(await context.RentalPaymentIntents.Where(item => item.LinesJson.Contains(ProductId.ToString("D")))
                .Select(item => item.OrderId).ToListAsync());
            orderIds = orderIds.Distinct().ToList();
            await context.BankTransferReceipts.Where(item => item.OrderId.HasValue && orderIds.Contains(item.OrderId.Value)).ExecuteDeleteAsync();
            await context.RentalPaymentIntents.Where(item => orderIds.Contains(item.OrderId)).ExecuteDeleteAsync();
            await context.InventoryLogs.Where(item => item.ProductId == ProductId).ExecuteDeleteAsync();
            await context.Rentals.Where(item => item.ProductId == ProductId).ExecuteDeleteAsync();
            await context.RentalTransactions.Where(item => orderIds.Contains(item.OrderId)).ExecuteDeleteAsync();
            await context.Payments.Where(item => orderIds.Contains(item.OrderId)).ExecuteDeleteAsync();
            await context.OrderDetails.Where(item => orderIds.Contains(item.OrderId)).ExecuteDeleteAsync();
            await context.Orders.Where(item => orderIds.Contains(item.Id)).ExecuteDeleteAsync();
            await context.Products.Where(item => item.Id == ProductId).ExecuteDeleteAsync();
        }
    }
}
