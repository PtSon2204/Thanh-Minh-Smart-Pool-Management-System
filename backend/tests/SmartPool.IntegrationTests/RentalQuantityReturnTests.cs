using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using SmartPool.Application.Features.ManageServices;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRental;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Queries.GetRentals;
using SmartPool.Application.Features.ManageServices.Queries.GetRentalDetails;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed class RentalQuantityReturnTests
{
    [Fact]
    public async Task GivenStockTen_WhenCheckingOutThreeAndReturningOneThenTwo_ThenStockAndLogsStayConsistent()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(0, 10);
        CheckoutRentalResponse? checkout = null;
        try
        {
            checkout = await fixture.CreateOperations().CheckoutRentalAsync(new CheckoutRentalCommand
            {
                ProductId = fixture.ProductId,
                Quantity = 3,
                OperatorId = fixture.OperatorId,
                DepositPerUnit = 0
            }, CancellationToken.None);

            Assert.Equal(7, await fixture.ReadStockAsync());
            var firstReturn = await fixture.CreateOperations().ReturnRentalQuantityAsync(
                new ReturnRentalQuantityCommand
                {
                    OrderId = checkout.OrderId,
                    ProductId = fixture.ProductId,
                    OperatorId = fixture.OperatorId,
                    RentalIds = checkout.Rentals.Take(1).Select(item => item.Id).ToList()
                }, CancellationToken.None);
            Assert.Equal((1, 8, 2), (firstReturn.ReturnedQuantity, firstReturn.StockQuantity, firstReturn.OutstandingQuantity));

            var remainingIds = checkout.Rentals.Skip(1).Select(item => item.Id).ToList();
            var secondCommand = new ReturnRentalQuantityCommand
            {
                OrderId = checkout.OrderId,
                ProductId = fixture.ProductId,
                OperatorId = fixture.OperatorId,
                RentalIds = remainingIds
            };
            var secondReturn = await fixture.CreateOperations().ReturnRentalQuantityAsync(secondCommand, CancellationToken.None);
            var replay = await fixture.CreateOperations().ReturnRentalQuantityAsync(secondCommand, CancellationToken.None);

            Assert.Equal((2, 10, 0), (secondReturn.ReturnedQuantity, secondReturn.StockQuantity, secondReturn.OutstandingQuantity));
            Assert.True(replay.AlreadyReturned);
            Assert.Equal(10, replay.StockQuantity);
            await using var context = fixture.CreateContext();
            var logs = await context.InventoryLogs.Where(log => log.ProductId == fixture.ProductId)
                .OrderBy(log => log.CreatedAt).Select(log => new { log.ChangeType, log.Quantity }).ToListAsync();
            Assert.Equal(new[] { ("RentalCheckout", -3), ("RentalReturn", 1), ("RentalReturn", 2) }, logs.Select(log => (log.ChangeType, log.Quantity)));
        }
        finally
        {
            if (checkout is not null)
            {
                await using var context = fixture.CreateContext();
                var rentalIds = checkout.Rentals.Select(item => item.Id).ToList();
                await context.Rentals.Where(rental => rentalIds.Contains(rental.Id)).ExecuteDeleteAsync();
                await context.OrderDetails.Where(detail => detail.Id == checkout.OrderDetailId).ExecuteDeleteAsync();
                await context.Orders.Where(order => order.Id == checkout.OrderId).ExecuteDeleteAsync();
            }
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenFreshThreeUnitRental_WhenGroupedListIsRead_ThenTotalsDescribeTheCompleteOrderProduct()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(3, 0);
        try
        {
            var grouped = await fixture.CreateOperations().GetRentalsAsync(
                new GetRentalsQuery { GroupByOrder = true, ProductId = fixture.ProductId, PageSize = 1 }, CancellationToken.None);

            var item = Assert.Single(grouped.Items);
            Assert.Equal(3, item.Quantity);
            Assert.Equal(0, item.ReturnedQuantity);
            Assert.Equal(3, item.OutstandingQuantity);
            Assert.Equal("Renting", item.Status);
            Assert.Equal(1, grouped.TotalCount);
            Assert.Equal(fixture.RentalIds.Order().ToArray(), item.OutstandingRentalIds);
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 3, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenPartiallyReturnedOrder_WhenDetailsAreRead_ThenSerializedDetailsRetainEveryUnit()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(4, 0);
        try
        {
            await fixture.CreateOperations().ReturnRentalQuantityAsync(
                fixture.QuantityCommand([fixture.RentalIds[1], fixture.RentalIds[3]]), CancellationToken.None);

            var grouped = await fixture.CreateOperations().GetRentalsAsync(new GetRentalsQuery
            {
                GroupByOrder = true, ProductId = fixture.ProductId, PageSize = 1
            }, CancellationToken.None);
            var groupedItem = Assert.Single(grouped.Items);
            Assert.Equal((4, 2, 2), (groupedItem.Quantity, groupedItem.ReturnedQuantity, groupedItem.OutstandingQuantity));
            Assert.Equal(1, grouped.TotalCount);
            Assert.Equal(fixture.RentalIds.Except([fixture.RentalIds[1], fixture.RentalIds[3]]).Order().ToArray(), groupedItem.OutstandingRentalIds);

            var details = await fixture.CreateOperations().GetRentalDetailsAsync(
                new GetRentalDetailsQuery { OrderId = fixture.OrderId, ProductId = fixture.ProductId }, CancellationToken.None);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(details, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var root = json.RootElement;

            Assert.Equal(fixture.OrderId, root.GetProperty("orderId").GetGuid());
            Assert.Equal(fixture.ProductId, root.GetProperty("productId").GetGuid());
            Assert.StartsWith("QA return ", root.GetProperty("productName").GetString());
            Assert.True(root.TryGetProperty("customerName", out _));
            Assert.True(root.TryGetProperty("customerPhone", out _));
            Assert.Equal("Pending", root.GetProperty("orderStatus").GetString());
            Assert.Equal(4, root.GetProperty("rentedQuantity").GetInt32());
            Assert.Equal(2, root.GetProperty("returnedQuantity").GetInt32());
            Assert.Equal(2, root.GetProperty("outstandingQuantity").GetInt32());
            Assert.Equal(0m, root.GetProperty("totalDepositAmount").GetDecimal());
            Assert.Equal(4, root.GetProperty("rentals").GetArrayLength());
            Assert.All(root.GetProperty("rentals").EnumerateArray(), rental =>
            {
                Assert.True(rental.TryGetProperty("rentTime", out _));
                Assert.True(rental.TryGetProperty("returnTime", out _));
                Assert.True(rental.TryGetProperty("depositAmount", out _));
                Assert.True(rental.TryGetProperty("status", out _));
            });
            Assert.Contains(root.GetProperty("rentals").EnumerateArray(), rental =>
                rental.GetProperty("status").GetString() == "Returned"
                && rental.GetProperty("returnTime").ValueKind != JsonValueKind.Null);
            Assert.Equal(fixture.RentalIds.Except([fixture.RentalIds[1], fixture.RentalIds[3]]).Order().ToArray(),
                root.GetProperty("outstandingRentalIds").EnumerateArray().Select(id => id.GetGuid()).ToArray());
            Assert.DoesNotContain("serial", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 4, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenMissingOrderProductPair_WhenDetailsAreRead_ThenRepositoryReturnsNotFound()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(1, 0);
        try
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.CreateOperations().GetRentalDetailsAsync(
                new GetRentalDetailsQuery { OrderId = fixture.OrderId, ProductId = Guid.NewGuid() }, CancellationToken.None));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 1, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenCompletedOrder_WhenDetailsAreRead_ThenReturnedHistoryRemainsAvailable()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(2, 0);
        try
        {
            await fixture.CreateOperations().ReturnRentalQuantityAsync(fixture.QuantityCommand(fixture.RentalIds), CancellationToken.None);
            await fixture.SetOrderStatusAsync("Completed");

            var details = await fixture.CreateOperations().GetRentalDetailsAsync(
                new GetRentalDetailsQuery { OrderId = fixture.OrderId, ProductId = fixture.ProductId }, CancellationToken.None);

            Assert.Equal("Completed", details.OrderStatus);
            Assert.Equal(2, details.RentedQuantity);
            Assert.Equal(2, details.ReturnedQuantity);
            Assert.Empty(details.OutstandingRentalIds);
            Assert.All(details.Rentals, rental => Assert.Equal("Returned", rental.Status));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 2, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenOneOrderWithTwoRentalProducts_WhenGroupedListIsRead_ThenProductsStayInSeparateGroups()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(2, 0);
        try
        {
            var secondProductId = await fixture.AddSecondProductRentalAsync();
            var grouped = await fixture.CreateOperations().GetRentalsAsync(
                new GetRentalsQuery { GroupByOrder = true, PageSize = 100 }, CancellationToken.None);
            var fixtureGroups = grouped.Items.Where(item => item.OrderId == fixture.OrderId).ToArray();

            Assert.Equal(2, fixtureGroups.Length);
            Assert.Equal(new[] { fixture.ProductId, secondProductId }.Order().ToArray(), fixtureGroups.Select(item => item.ProductId).Order().ToArray());
            Assert.All(fixtureGroups, item => Assert.Equal(2, item.Quantity));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 4, 2, 1, 2), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenFreshTwoUnitRental_WhenReturningBothAndReplaying_ThenStockAndLogChangeOnlyOnce()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(2, 3);
        try
        {
            var command = fixture.QuantityCommand(fixture.RentalIds);
            var response = await fixture.CreateOperations().ReturnRentalQuantityAsync(command, CancellationToken.None);
            var replay = await fixture.CreateOperations().ReturnRentalQuantityAsync(command, CancellationToken.None);

            Assert.Equal(2, response.ReturnedQuantity);
            Assert.False(response.AlreadyReturned);
            Assert.Equal(5, response.StockQuantity);
            Assert.Equal((2, 2, 0), (response.RentedQuantity, response.TotalReturnedQuantity, response.OutstandingQuantity));
            Assert.Equal(0, replay.ReturnedQuantity);
            Assert.True(replay.AlreadyReturned);
            Assert.Equal(5, replay.StockQuantity);
            Assert.Equal(1, await fixture.ReadReturnLogCountAsync());
            Assert.Equal(2, await fixture.ReadReturnLogQuantityAsync());
            using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.Equal(fixture.OrderId, serialized.RootElement.GetProperty("orderId").GetGuid());
            Assert.Equal(fixture.ProductId, serialized.RootElement.GetProperty("productId").GetGuid());
            Assert.Equal(2, serialized.RootElement.GetProperty("returnedQuantity").GetInt32());
            Assert.False(serialized.RootElement.GetProperty("alreadyReturned").GetBoolean());
            Assert.Equal(5, serialized.RootElement.GetProperty("stockQuantity").GetInt32());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 2, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenTwoOverlappingReturns_WhenRunConcurrently_ThenOneBatchCommitsWithoutPartialStockChange()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(3, 1);
        try
        {
            var ids = fixture.RentalIds;
            var firstIds = new List<Guid> { ids[0], ids[1] };
            var secondIds = new List<Guid> { ids[1], ids[2] };
            using var gate = new Barrier(2);
            async Task<(bool Success, int Count)> Submit(List<Guid> selected)
            {
                await using var context = fixture.CreateContext();
                var operations = fixture.CreateOperations(context);
                await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                try
                {
                    var result = await operations.ReturnRentalQuantityAsync(
                        fixture.QuantityCommand(selected), CancellationToken.None);
                    return (true, result.ReturnedQuantity);
                }
                catch (ServiceConflictException)
                {
                    return (false, 0);
                }
            }

            var outcomes = await Task.WhenAll(Submit(firstIds), Submit(secondIds));

            Assert.Single(outcomes, outcome => outcome.Success && outcome.Count == 2);
            Assert.Single(outcomes, outcome => !outcome.Success);
            Assert.Equal(2, await fixture.ReadReturnedCountAsync());
            Assert.Equal(3, await fixture.ReadStockAsync());
            Assert.Equal(1, await fixture.ReadReturnLogCountAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 3, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenSingleUnitAndQuantityReturns_WhenTheyContend_ThenBothUseProductThenRentalLocks()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(2, 2);
        try
        {
            using var gate = new Barrier(2);
            async Task<bool> Single()
            {
                await using var context = fixture.CreateContext();
                await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                try
                {
                    await fixture.CreateOperations(context).ReturnRentalAsync(
                        new ReturnRentalCommand { RentalId = fixture.RentalIds[0], OperatorId = fixture.OperatorId },
                        CancellationToken.None);
                    return true;
                }
                catch (ServiceConflictException)
                {
                    return false;
                }
            }

            async Task<int> Batch()
            {
                await using var context = fixture.CreateContext();
                await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                try
                {
                    var result = await fixture.CreateOperations(context).ReturnRentalQuantityAsync(
                        fixture.QuantityCommand([fixture.RentalIds[0], fixture.RentalIds[1]]), CancellationToken.None);
                    return result.ReturnedQuantity;
                }
                catch (ServiceConflictException)
                {
                    return 0;
                }
            }

            var singleTask = Single();
            var batchTask = Batch();
            await Task.WhenAll(singleTask, batchTask);
            var outcomes = (Single: await singleTask, Batch: await batchTask);

            var returnedCount = outcomes.Single ? 1 : outcomes.Batch;
            Assert.Equal(outcomes.Single ? 0 : 2, outcomes.Batch);
            Assert.Equal(returnedCount, await fixture.ReadReturnedCountAsync());
            Assert.Equal(2 + returnedCount, await fixture.ReadStockAsync());
            Assert.Equal(returnedCount, await fixture.ReadReturnLogQuantityAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 2, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenExistingSingleReturnContract_WhenReturningOneRental_ThenResponseStockAndLogRemainSingleUnit()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(1, 1);
        try
        {
            var response = await fixture.CreateOperations().ReturnRentalAsync(
                new ReturnRentalCommand { RentalId = fixture.RentalIds[0], OperatorId = fixture.OperatorId },
                CancellationToken.None);

            Assert.Equal(fixture.OrderId, response.Rental.OrderId);
            Assert.Equal(fixture.ProductId, response.Rental.ProductId);
            Assert.Equal(fixture.RentalIds[0], response.Rental.Id);
            Assert.Equal("Returned", response.Rental.Status);
            Assert.Equal(2, response.StockQuantity);
            Assert.Equal(1, await fixture.ReadReturnLogQuantityAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 1, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenMixedReturnedAndRentingSelection_WhenReturning_ThenItConflictsWithoutChangingAnything()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(2, 2);
        try
        {
            await fixture.CreateOperations().ReturnRentalAsync(
                new ReturnRentalCommand { RentalId = fixture.RentalIds[0], OperatorId = fixture.OperatorId },
                CancellationToken.None);
            var beforeStock = await fixture.ReadStockAsync();
            var beforeLogCount = await fixture.ReadReturnLogCountAsync();

            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.CreateOperations()
                .ReturnRentalQuantityAsync(fixture.QuantityCommand(fixture.RentalIds), CancellationToken.None));

            Assert.Equal(beforeStock, await fixture.ReadStockAsync());
            Assert.Equal(beforeLogCount, await fixture.ReadReturnLogCountAsync());
            Assert.Equal(1, await fixture.ReadReturnedCountAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 2, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenForeignOrderOrProductSelection_WhenReturning_ThenItConflictsWithoutDisclosure()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(1, 1);
        try
        {
            var wrongOrder = fixture.QuantityCommand(fixture.RentalIds);
            wrongOrder.OrderId = Guid.NewGuid();
            var wrongProduct = fixture.QuantityCommand(fixture.RentalIds);
            wrongProduct.ProductId = Guid.NewGuid();

            await Assert.ThrowsAsync<ServiceConflictException>(() => fixture.CreateOperations()
                .ReturnRentalQuantityAsync(wrongOrder, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.CreateOperations()
                .ReturnRentalQuantityAsync(wrongProduct, CancellationToken.None));

            Assert.Equal(1, await fixture.ReadStockAsync());
            Assert.Equal(0, await fixture.ReadReturnLogCountAsync());
            Assert.Equal(0, await fixture.ReadReturnedCountAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 1, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenMissingRentalIdentity_WhenReturning_ThenItReturnsNotFoundWithoutChanges()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(1, 1);
        try
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.CreateOperations()
                .ReturnRentalQuantityAsync(fixture.QuantityCommand([Guid.NewGuid()]), CancellationToken.None));

            Assert.Equal(1, await fixture.ReadStockAsync());
            Assert.Equal(0, await fixture.ReadReturnLogCountAsync());
            Assert.Equal(0, await fixture.ReadReturnedCountAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 1, 1, 1, 1), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenDisjointBatches_WhenReturnedConcurrently_ThenBothCommitAndEachWriteOneLog()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(4, 0);
        try
        {
            using var gate = new Barrier(2);
            async Task<ReturnRentalQuantityResponse> Submit(List<Guid> ids)
            {
                await using var context = fixture.CreateContext();
                await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                return await fixture.CreateOperations(context).ReturnRentalQuantityAsync(
                    fixture.QuantityCommand(ids), CancellationToken.None);
            }

            var results = await Task.WhenAll(
                Submit([fixture.RentalIds[0], fixture.RentalIds[1]]),
                Submit([fixture.RentalIds[2], fixture.RentalIds[3]]));

            Assert.All(results, result => Assert.Equal(2, result.ReturnedQuantity));
            Assert.Equal(4, await fixture.ReadStockAsync());
            Assert.Equal(4, await fixture.ReadReturnedCountAsync());
            Assert.Equal(2, await fixture.ReadReturnLogCountAsync());
            Assert.Equal(4, await fixture.ReadReturnLogQuantityAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((2, 4, 1, 1, 1), fixture.CleanupCounts);
        }
    }
}

internal sealed class RentalReturnFixture : IAsyncDisposable
{
    private readonly DbContextOptions<SmartPoolDbContext> _options;
    private readonly IMapper _mapper;
    private bool _setupComplete;
    private bool _cleaned;
    private readonly SmartPoolDbContext _context;

    private RentalReturnFixture(DbContextOptions<SmartPoolDbContext> options, Guid operatorId, int count)
    {
        _options = options;
        _context = new SmartPoolDbContext(options);
        _mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        OperatorId = operatorId;
        ProductId = Guid.NewGuid();
        OrderId = Guid.NewGuid();
        RentalIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
    }

    public Guid OperatorId { get; }
    public Guid ProductId { get; }
    public Guid? SecondProductId { get; private set; }
    public Guid OrderId { get; }
    public List<Guid> RentalIds { get; }
    public (int Logs, int Rentals, int Details, int Orders, int Products)? CleanupCounts { get; private set; }

    public static async Task<RentalReturnFixture> CreateAsync(int rentalCount, int stockQuantity)
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var context = new SmartPoolDbContext(options);
        var operatorId = await context.Users.AsNoTracking().Select(user => user.Id).FirstOrDefaultAsync();
        if (operatorId == Guid.Empty) throw new InvalidOperationException("Guarded database requires an existing operator foreign key.");
        var fixture = new RentalReturnFixture(options, operatorId, rentalCount);
        try
        {
            var now = DateTime.UtcNow;
            context.Products.Add(new Product
            {
                Id = fixture.ProductId, Name = $"QA return {fixture.ProductId:N}", Type = "Rental",
                Price = 1, StockQuantity = stockQuantity, IsActive = true, IsDeleted = false,
                CreatedAt = now, UpdatedAt = now
            });
            context.Orders.Add(new Order
            {
                Id = fixture.OrderId, UserId = operatorId, TotalAmount = 1, FinalAmount = 1,
                DiscountAmount = 0, Status = "Pending", IsDeleted = false, CreatedAt = now, UpdatedAt = now
            });
            context.OrderDetails.Add(new OrderDetail
            {
                Id = Guid.NewGuid(), OrderId = fixture.OrderId, ItemType = "Product",
                ItemId = fixture.ProductId, Quantity = fixture.RentalIds.Count, UnitPrice = 1, CreatedAt = now
            });
            context.Rentals.AddRange(fixture.RentalIds.Select(id => new Rental
            {
                Id = id, OrderId = fixture.OrderId, ProductId = fixture.ProductId,
                RentTime = now, DepositAmount = 0, Status = "Renting", UpdatedAt = now
            }));
            await context.SaveChangesAsync();
            fixture._setupComplete = true;
            return fixture;
        }
        catch
        {
            await fixture.CleanupAsync();
            throw;
        }
    }

    public SmartPoolDbContext CreateContext() => new(_options);
    public ServiceOperations CreateOperations(SmartPoolDbContext? context = null) => new(context ?? _context, _mapper);
    public ReturnRentalQuantityCommand QuantityCommand(List<Guid> ids) => new()
    {
        OrderId = OrderId, ProductId = ProductId, OperatorId = OperatorId, RentalIds = ids
    };

    public async Task<int> ReadStockAsync()
    {
        await using var context = CreateContext();
        return await context.Products.Where(item => item.Id == ProductId).Select(item => item.StockQuantity!.Value).SingleAsync();
    }

    public async Task<int> ReadReturnedCountAsync()
    {
        await using var context = CreateContext();
        return await context.Rentals.CountAsync(item => RentalIds.Contains(item.Id) && item.Status == "Returned");
    }

    public async Task<int> ReadReturnLogCountAsync()
    {
        await using var context = CreateContext();
        return await context.InventoryLogs.CountAsync(log => log.ProductId == ProductId && log.ChangeType == "RentalReturn");
    }

    public async Task<int> ReadReturnLogQuantityAsync()
    {
        await using var context = CreateContext();
        return await context.InventoryLogs.Where(log => log.ProductId == ProductId && log.ChangeType == "RentalReturn").SumAsync(log => log.Quantity);
    }

    public async Task SetOrderStatusAsync(string status)
    {
        await using var context = CreateContext();
        await context.Orders.Where(order => order.Id == OrderId)
            .ExecuteUpdateAsync(update => update.SetProperty(order => order.Status, status));
    }

    public async Task<Guid> AddSecondProductRentalAsync()
    {
        var productId = Guid.NewGuid();
        var rentalIds = Enumerable.Range(0, 2).Select(_ => Guid.NewGuid()).ToArray();
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        context.Products.Add(new Product
        {
            Id = productId, Name = $"QA return {productId:N}", Type = "Rental", Price = 1,
            StockQuantity = 0, IsActive = true, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        context.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(), OrderId = OrderId, ItemType = "Product", ItemId = productId,
            Quantity = rentalIds.Length, UnitPrice = 1, CreatedAt = now
        });
        context.Rentals.AddRange(rentalIds.Select(rentalId => new Rental
        {
            Id = rentalId, OrderId = OrderId, ProductId = productId, RentTime = now,
            DepositAmount = 0, Status = "Renting", UpdatedAt = now
        }));
        await context.SaveChangesAsync();
        RentalIds.AddRange(rentalIds);
        SecondProductId = productId;
        return productId;
    }

    public async Task CleanupAsync()
    {
        if (_cleaned) return;
        await using var context = new SmartPoolDbContext(_options);
        var fixtureLogs = context.InventoryLogs.Where(log => log.ProductId == ProductId);
        var expectedLogs = await fixtureLogs.CountAsync();
        var logs = await fixtureLogs.ExecuteDeleteAsync();
        var rentals = await context.Rentals.Where(item => RentalIds.Contains(item.Id)).ExecuteDeleteAsync();
        var expectedDetails = SecondProductId.HasValue ? 2 : 1;
        var details = await context.OrderDetails.Where(item => item.OrderId == OrderId).ExecuteDeleteAsync();
        var orders = await context.Orders.Where(item => item.Id == OrderId).ExecuteDeleteAsync();
        var products = await context.Products.Where(item => item.Id == ProductId).ExecuteDeleteAsync();
        if (SecondProductId.HasValue)
            products += await context.Products.Where(item => item.Id == SecondProductId.Value).ExecuteDeleteAsync();
        if (logs != expectedLogs || (_setupComplete &&
            (rentals != RentalIds.Count || details != expectedDetails || orders != 1 || products != expectedDetails)))
            throw new InvalidOperationException($"Rental fixture cleanup count mismatch: {logs}/{rentals}/{details}/{orders}/{products}.");
        CleanupCounts = (logs, rentals, details, orders, products);
        _cleaned = true;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
        await _context.DisposeAsync();
    }

}
