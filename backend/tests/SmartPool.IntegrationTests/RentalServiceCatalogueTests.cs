using SmartPool.Infrastructure.Persistence.Repositories.Services;
using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Features.ManageServices.Commands.CreateService;
using SmartPool.Application.Features.ManageServices.Commands.ReturnRentalQuantity;
using SmartPool.Application.Features.ManageServices.Mapping;
using SmartPool.Application.Features.ManageServices.Queries.GetInventoryHistory;
using SmartPool.Application.Features.ManageServices.Queries.GetServices;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed class RentalServiceCatalogueTests
{
    [Fact]
    public async Task NewlyCreatedRentalItemCanBeListedRentedAndReturnedWithItsInitialQuantity()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var context = new SmartPoolDbContext(options);
        var mapper = CreateMapper();
        var name = $"Rental catalogue {Guid.NewGuid():N}";
        var handler = CreateHandler(context, mapper);

        try
        {
            var created = await handler.Handle(new CreateServiceCommand
            {
                Name = $" {name} ", Price = 20000m, StockQuantity = 5
            }, default);
            var operatorId = await context.Users.Select(user => user.Id).FirstAsync();
            var operations = new ServiceOperations(context, mapper);
            var listed = await new GetServicesHandler(new Repository<Product>(context), mapper, new GetServicesValidator())
                .Handle(new GetServicesQuery { SearchTerm = name, IsActive = true }, default);

            Assert.Equal(name, Assert.Single(listed.Items).Name);
            Assert.Equal(5, created.StockQuantity);
            var checkout = await operations.CheckoutRentalAsync(new CheckoutRentalCommand
            {
                ProductId = created.Id, Quantity = 2, DepositPerUnit = 10000m, OperatorId = operatorId
            }, default);

            Assert.Equal(40000m, checkout.FinalAmount);
            Assert.Equal(2, checkout.Rentals.Count);
            Assert.All(checkout.Rentals, rental => Assert.Equal(10000m, rental.DepositAmount));
            Assert.Equal(3, await context.Products.AsNoTracking().Where(item => item.Id == created.Id)
                .Select(item => item.StockQuantity).SingleAsync());

            var returned = await operations.ReturnRentalQuantityAsync(new ReturnRentalQuantityCommand
            {
                OrderId = checkout.OrderId, ProductId = created.Id, OperatorId = operatorId,
                RentalIds = checkout.Rentals.Select(rental => rental.Id).ToList()
            }, default);
            Assert.Equal(5, returned.StockQuantity);
            Assert.Equal(0, returned.OutstandingQuantity);

            var history = await operations.GetInventoryHistoryAsync(new GetInventoryHistoryQuery
                { ServiceId = created.Id }, default);
            Assert.Equal(2, history.TotalCount);
            Assert.Contains(history.Items, log => log.ChangeType == "RentalCheckout" && log.Quantity == -2);
            Assert.Contains(history.Items, log => log.ChangeType == "RentalReturn" && log.Quantity == 2);
        }
        finally
        {
            await using var cleanup = new SmartPoolDbContext(options);
            var products = cleanup.Products.Where(product => product.Name == name).Select(product => product.Id);
            var orders = await cleanup.Rentals.Where(rental => products.Contains(rental.ProductId))
                .Select(rental => rental.OrderId).Distinct().ToArrayAsync();
            await cleanup.InventoryLogs.Where(log => products.Contains(log.ProductId)).ExecuteDeleteAsync();
            await cleanup.Rentals.Where(rental => products.Contains(rental.ProductId)).ExecuteDeleteAsync();
            await cleanup.OrderDetails.Where(detail => orders.Contains(detail.OrderId)).ExecuteDeleteAsync();
            await cleanup.Orders.Where(order => orders.Contains(order.Id)).ExecuteDeleteAsync();
            await cleanup.Products.Where(product => product.Name == name).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public async Task MissingOrNegativeInitialQuantityDoesNotCreateARentalItem(int? quantity)
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var context = new SmartPoolDbContext(options);
        var name = $"Invalid rental {Guid.NewGuid():N}";
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(context, CreateMapper()).Handle(
            new CreateServiceCommand { Name = name, Price = 10000m, StockQuantity = quantity }, default));
        Assert.False(await context.Products.AnyAsync(product => product.Name == name));
    }

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
        configuration.AddProfile<ServiceMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static CreateServiceHandler CreateHandler(SmartPoolDbContext context, IMapper mapper) => new(
        new Repository<Product>(context), new UnitOfWork(context), mapper, new CreateServiceValidator());
}
