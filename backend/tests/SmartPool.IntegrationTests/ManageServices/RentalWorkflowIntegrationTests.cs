using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.QuoteRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Services;

namespace SmartPool.IntegrationTests.ManageServices;

public sealed class RentalWorkflowIntegrationTests
{
    [Fact]
    public async Task QuoteCalculatesFeesAndFixedDepositsWithoutIssuingStock()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var result = await fixture.Operations(db).QuoteRentalAsync(new QuoteRentalCommand
        {
            Items = [new() { ProductId = fixture.FirstId, Quantity = 2 }, new() { ProductId = fixture.SecondId, Quantity = 1 }]
        }, default);
        Assert.Equal(20000m, result.DepositPerUnit);
        Assert.Equal((35000m, 60000m, 95000m), (result.RentalFeeTotal, result.DepositTotal, result.TotalToCollect));
        Assert.Equal(5, await db.Products.Where(product => product.Id == fixture.FirstId).Select(product => product.StockQuantity).SingleAsync());
        Assert.False(await db.Rentals.AnyAsync(rental => rental.ProductId == fixture.FirstId || rental.ProductId == fixture.SecondId));
    }

    [Fact]
    public async Task UnavailableSecondLineRollsBackAllLinesAndCollection()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var request = fixture.Request();
        request.Items.Add(new() { ProductId = fixture.SecondId, Quantity = 5, ExpectedUnitPrice = 15000 });
        await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.Operations(db).CheckoutRentalAsync(request, default));
        Assert.Equal(5, await db.Products.Where(product => product.Id == fixture.FirstId).Select(product => product.StockQuantity).SingleAsync());
        Assert.False(await db.Rentals.AnyAsync(rental => rental.ProductId == fixture.FirstId));
        Assert.False(await db.RentalTransactions.AnyAsync(item => item.Id == request.RequestId));
    }

    [Fact]
    public async Task RepeatedCollectionReplaysOneOutcomeAndChangedPayloadConflicts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request();
        CheckoutRentalResponse first;
        await using (var db = fixture.Context()) first = await fixture.Operations(db).CheckoutRentalAsync(request, default);
        await using (var db = fixture.Context())
        {
            var replay = await fixture.Operations(db).CheckoutRentalAsync(request, default);
            Assert.Equal(first.OrderId, replay.OrderId);
            Assert.Equal((20000m, 40000m, 60000m), (first.RentalFeeTotal, first.DepositTotal, first.TotalCollected));
            Assert.All(first.Rentals, rental => Assert.Equal(20000m, rental.DepositAmount));
            Assert.Single(await db.RentalTransactions.Where(item => item.OrderId == first.OrderId).ToListAsync());
            var payment = await db.Payments.SingleAsync(item => item.OrderId == first.OrderId);
            Assert.Equal(20000m, payment.Amount);
            Assert.Equal("CASH", payment.PaymentMethod);
            Assert.Null((await db.Orders.AsNoTracking().SingleAsync(order => order.Id == first.OrderId)).UserId);
            request.CustomerName = "Different customer";
            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.Operations(db).CheckoutRentalAsync(request, default));
        }
    }

    [Fact]
    public async Task MixedResolutionRefundsOnlyNormalUnitsAndCompletesTheOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        CheckoutRentalResponse issue;
        await using (var db = fixture.Context()) issue = await fixture.Operations(db).CheckoutRentalAsync(fixture.Request(3), default);
        await using var context = fixture.Context();
        var request = new ReturnRentalQuantityCommand
        {
            RequestId = Guid.NewGuid(), OrderId = issue.OrderId, ProductId = fixture.FirstId, OperatorId = fixture.ActorId,
            RentalIds = [issue.Rentals[0].Id], DamagedRentalIds = [issue.Rentals[1].Id], LostRentalIds = [issue.Rentals[2].Id],
            Reason = "One damaged and one lost", SettlementConfirmed = true
        };
        var result = await fixture.Operations(context).ReturnRentalQuantityAsync(request, default);
        Assert.Equal((20000m, 40000m, 3, "Completed"), (result.RefundAmount, result.RetainedAmount, result.StockQuantity, result.OrderStatus));
        var lost = await context.Rentals.AsNoTracking().SingleAsync(rental => rental.Id == issue.Rentals[2].Id);
        Assert.Equal("Lost", lost.Status);
        Assert.Null(lost.ReturnTime);
        Assert.NotNull(lost.ResolvedAtUtc);
        var replay = await fixture.Operations(context).ReturnRentalQuantityAsync(request, default);
        Assert.Equal(result.RequestId, replay.RequestId);
        Assert.Equal(2, await context.RentalTransactions.CountAsync(item => item.OrderId == issue.OrderId));
        Assert.Equal(3, await context.Products.Where(product => product.Id == fixture.FirstId).Select(product => product.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task SingleDamagedReturnMapsToTheRentalState()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var issue = await fixture.Operations(db).CheckoutRentalAsync(fixture.Request(1), default);
        var response = await fixture.Operations(db).ReturnRentalAsync(new ReturnRentalCommand
        {
            RequestId = Guid.NewGuid(), RentalId = issue.Rentals[0].Id, OperatorId = fixture.ActorId,
            Condition = "Damaged", Reason = "Condition recorded", SettlementConfirmed = true
        }, default);

        Assert.Equal("Damaged", response.Rental.Status);
        Assert.NotNull(response.Rental.ReturnTime);
        Assert.Equal(4, response.StockQuantity);
    }

    private sealed class Fixture(DbContextOptions<SmartPoolDbContext> options, Guid actorId) : IAsyncDisposable
    {
        public Guid ActorId { get; } = actorId;
        public Guid FirstId { get; } = Guid.NewGuid();
        public Guid SecondId { get; } = Guid.NewGuid();
        public SmartPoolDbContext Context() => new(options);
        public ServiceOperations Operations(SmartPoolDbContext db) => new(db,
            new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper(), TimeProvider.System, Options.Create(new SePayOptions()));
        public CheckoutRentalCommand Request(int quantity = 2) => new()
        {
            RequestId = Guid.NewGuid(), CustomerName = "QA customer", CustomerPhone = "0901234567", OperatorId = ActorId,
            CollectionConfirmed = true, Items = [new() { ProductId = FirstId, Quantity = quantity, ExpectedUnitPrice = 10000 }]
        };
        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            await using var db = new SmartPoolDbContext(options);
            var fixture = new Fixture(options, await db.Users.Select(user => user.Id).FirstAsync());
            db.Products.AddRange(
                new Product { Id = fixture.FirstId, Name = $"QA flow {fixture.FirstId:N}", Price = 10000, StockQuantity = 5, IsActive = true, IsDeleted = false },
                new Product { Id = fixture.SecondId, Name = $"QA flow {fixture.SecondId:N}", Price = 15000, StockQuantity = 4, IsActive = true, IsDeleted = false });
            await db.SaveChangesAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            var ids = new[] { FirstId, SecondId };
            var orders = await db.Rentals.Where(rental => ids.Contains(rental.ProductId)).Select(rental => rental.OrderId).Distinct().ToListAsync();
            await db.InventoryLogs.Where(log => ids.Contains(log.ProductId)).ExecuteDeleteAsync();
            await db.Rentals.Where(rental => ids.Contains(rental.ProductId)).ExecuteDeleteAsync();
            await db.RentalTransactions.Where(item => orders.Contains(item.OrderId)).ExecuteDeleteAsync();
            await db.Payments.Where(payment => orders.Contains(payment.OrderId)).ExecuteDeleteAsync();
            await db.OrderDetails.Where(detail => orders.Contains(detail.OrderId)).ExecuteDeleteAsync();
            await db.Orders.Where(order => orders.Contains(order.Id)).ExecuteDeleteAsync();
            await db.Products.Where(product => ids.Contains(product.Id)).ExecuteDeleteAsync();
        }
    }
}
