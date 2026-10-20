using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.GoodsReceipts;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed class GoodsReceiptIntegrationTests
{
    [Fact]
    public async Task DifferentImportCostsArePreservedAndDoNotChangeSellingPrice()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var first = await fixture.CreateAsync(20, 50000);
        Assert.Equal(10, await fixture.StockAsync()); // Draft never changes stock.
        await fixture.PostAsync(first.Id);
        var second = await fixture.CreateAsync(15, 60000);
        await fixture.PostAsync(second.Id);

        await using var context = fixture.Context();
        var product = await context.Products.SingleAsync(p => p.Id == fixture.ProductId);
        Assert.Equal(45, product.StockQuantity);
        Assert.Equal(90000m, product.Price);
        var costs = await context.GoodsReceiptDetails.Where(d => d.ProductId == fixture.ProductId).Select(d => d.UnitCost).OrderBy(cost => cost).ToListAsync();
        Assert.Equal(new[] { 50000m, 60000m }, costs);
        var logs = await context.InventoryLogs.Where(l => l.ProductId == fixture.ProductId).Include(l => l.ReceiptDetail).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => { Assert.NotNull(log.ReceiptDetail); Assert.Equal(log.Quantity, log.ReceiptDetail!.Quantity); });
        var history = await new ServiceOperations(context, null!).GetInventoryHistoryAsync(
            new GetInventoryHistoryQuery { ServiceId = fixture.ProductId }, default);
        Assert.All(history.Items, item => { Assert.NotNull(item.ReceiptId); Assert.NotNull(item.ReceiptCode); Assert.NotNull(item.UnitCost); });
        var listed = await fixture.Operations(context).GetAllAsync(new GetGoodsReceiptsQuery { ProductId = fixture.ProductId, Status = "Posted" }, default);
        Assert.Equal(2, listed.TotalCount);
        Assert.Equal(1900000m, listed.Items.Sum(item => item.TotalAmount));
    }

    [Fact]
    public async Task RetriedCreateAndConcurrentPostingDoNotDuplicateStockOrLogs()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var command = fixture.Command(5, 12345.67m);
        await using (var context = fixture.Context()) await fixture.Operations(context).CreateAsync(command, default);
        await using (var context = fixture.Context()) await fixture.Operations(context).CreateAsync(command, default);
        await Task.WhenAll(fixture.PostAsync(command.Id), fixture.PostAsync(command.Id));
        await fixture.PostAsync(command.Id);
        Assert.Equal(15, await fixture.StockAsync());
        await using var read = fixture.Context();
        Assert.Equal(1, await read.InventoryLogs.CountAsync(l => l.ProductId == fixture.ProductId));
        Assert.Equal(1, await read.GoodsReceiptDetails.CountAsync(d => d.ReceiptId == command.Id));
    }

    [Fact]
    public async Task DuplicateCodeAndReusingRequestIdWithDifferentDataAreRejected()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var command = fixture.Command(1, 100);
        await using (var context = fixture.Context()) await fixture.Operations(context).CreateAsync(command, default);
        var originalId = command.Id;
        command.Id = Guid.NewGuid();
        await using (var context = fixture.Context())
            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.Operations(context).CreateAsync(command, default));
        command.Id = originalId;
        command.Details[0].UnitCost = 200;
        await using (var context = fixture.Context())
            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.Operations(context).CreateAsync(command, default));
        Assert.Equal(10, await fixture.StockAsync());
    }

    [Fact]
    public async Task CancelledDraftCannotBePostedAndPostedReceiptCannotBeCancelled()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var draft = await fixture.CreateAsync(2, 100);
        await using (var context = fixture.Context()) await fixture.Operations(context).CancelAsync(draft.Id, default);
        await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.PostAsync(draft.Id));
        Assert.Equal(10, await fixture.StockAsync());
        var posted = await fixture.CreateAsync(3, 200);
        await fixture.PostAsync(posted.Id);
        await using (var context = fixture.Context())
            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.Operations(context).CancelAsync(posted.Id, default));
        Assert.Equal(13, await fixture.StockAsync());
    }

    [Fact]
    public async Task InvalidSecondProductRollsBackWholeReceiptIncludingEarlierStockChange()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var secondProductId = Guid.NewGuid();
        // Determine provider GUID ordering from the database, not .NET's GUID ordering.
        await using (var context = fixture.Context())
        {
            context.Products.Add(new Product { Id = secondProductId, Name = "Second", Type = "Sale", Price = 100, StockQuantity = 10, IsDeleted = false });
            await context.SaveChangesAsync();
        }
        try
        {
            var command = fixture.Command(2, 100);
            command.Details.Add(new GoodsReceiptLine { ProductId = secondProductId, Quantity = 3, UnitCost = 200 });
            await using (var context = fixture.Context()) await fixture.Operations(context).CreateAsync(command, default);
            Guid lastProduct;
            await using (var context = fixture.Context())
            {
                lastProduct = await context.Products.Where(p => p.Id == secondProductId || p.Id == fixture.ProductId).OrderByDescending(p => p.Id).Select(p => p.Id).FirstAsync();
                await context.Products.Where(p => p.Id == lastProduct).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true));
            }
            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.PostAsync(command.Id));
            await using var read = fixture.Context();
            Assert.Equal("Draft", await read.GoodsReceipts.Where(r => r.Id == command.Id).Select(r => r.Status).SingleAsync());
            Assert.Equal(0, await read.InventoryLogs.CountAsync(l => l.ProductId == fixture.ProductId || l.ProductId == secondProductId));
            Assert.All(await read.Products.Where(p => p.Id == fixture.ProductId || p.Id == secondProductId).ToListAsync(), p => Assert.Equal(10, p.StockQuantity));
        }
        finally
        {
            await using var cleanup = fixture.Context();
            var receiptIds = await cleanup.GoodsReceiptDetails.Where(d => d.ProductId == secondProductId).Select(d => d.ReceiptId).ToArrayAsync();
            await cleanup.GoodsReceiptDetails.Where(d => receiptIds.Contains(d.ReceiptId)).ExecuteDeleteAsync();
            await cleanup.GoodsReceipts.Where(r => receiptIds.Contains(r.Id)).ExecuteDeleteAsync();
            await cleanup.Products.Where(p => p.Id == secondProductId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task PostingCannotOverflowStock()
    {
        await using var fixture = await ReceiptFixture.CreateAsync();
        var receipt = await fixture.CreateAsync(1, 100);
        await using (var context = fixture.Context())
            await context.Products.Where(p => p.Id == fixture.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, int.MaxValue));
        await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.PostAsync(receipt.Id));
        Assert.Equal(int.MaxValue, await fixture.StockAsync());
    }

    private sealed class ReceiptFixture(DbContextOptions<SmartPoolDbContext> options) : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid ProductId { get; } = Guid.NewGuid();
        public SmartPoolDbContext Context() => new(options);
        public GoodsReceiptOperations Operations(SmartPoolDbContext context) => new(context, TimeProvider.System);

        public static async Task<ReceiptFixture> CreateAsync()
        {
            var fixture = new ReceiptFixture(await IntegrationTestDatabase.CreateOptionsAsync());
            await using var context = fixture.Context();
            context.Users.Add(new User { Id = fixture.UserId, Username = $"receipt-{fixture.UserId:N}", Status = "Active", IsDeleted = false });
            context.Products.Add(new Product { Id = fixture.ProductId, Name = "Receipt test product", Type = "Sale", Price = 90000, StockQuantity = 10, IsDeleted = false, IsActive = true });
            await context.SaveChangesAsync();
            return fixture;
        }

        public CreateGoodsReceiptCommand Command(int quantity, decimal cost) => new()
        {
            Id = Guid.NewGuid(), ReceiptCode = $"PN-{Guid.NewGuid():N}", ReceivedAt = DateTime.UtcNow,
            OperatorId = UserId, Details = [new() { ProductId = ProductId, Quantity = quantity, UnitCost = cost }]
        };
        public async Task<GoodsReceiptResponse> CreateAsync(int quantity, decimal cost)
        {
            await using var context = Context();
            return await Operations(context).CreateAsync(Command(quantity, cost), default);
        }
        public async Task<GoodsReceiptResponse> PostAsync(Guid id)
        {
            await using var context = Context();
            return await Operations(context).PostAsync(id, UserId, default);
        }
        public async Task<int?> StockAsync()
        {
            await using var context = Context();
            return await context.Products.Where(p => p.Id == ProductId).Select(p => p.StockQuantity).SingleAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await using var context = Context();
            await context.InventoryLogs.Where(l => l.ProductId == ProductId).ExecuteDeleteAsync();
            var receipts = context.GoodsReceipts.Where(r => r.CreatedBy == UserId).Select(r => r.Id);
            await context.GoodsReceiptDetails.Where(d => receipts.Contains(d.ReceiptId)).ExecuteDeleteAsync();
            await context.GoodsReceipts.Where(r => r.CreatedBy == UserId).ExecuteDeleteAsync();
            await context.Products.Where(p => p.Id == ProductId).ExecuteDeleteAsync();
            await context.Users.Where(u => u.Id == UserId).ExecuteDeleteAsync();
        }
    }
}
