using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;

namespace SmartPool.IntegrationTests;

public sealed class ServiceStatusIntegrationTests
{
    [Fact]
    public async Task GivenTrackedService_WhenSettingStatus_ThenOnlyActivityStatusAndUpdateTimeChange()
    {
        await using var fixture = await RentalReturnFixture.CreateAsync(0, 10);
        try
        {
            await using var context = fixture.CreateContext();
            var before = await context.Products.AsNoTracking()
                .Where(product => product.Id == fixture.ProductId)
                .Select(product => new { product.Name, product.Type, product.Price, product.StockQuantity, product.IsActive, product.IsDeleted, product.CreatedAt, product.UpdatedAt })
                .SingleAsync();

            var result = await fixture.CreateOperations().SetServiceStatusAsync(
                new SetServiceStatusCommand { Id = fixture.ProductId, IsActive = false }, CancellationToken.None);

            var after = await context.Products.AsNoTracking()
                .Where(product => product.Id == fixture.ProductId)
                .Select(product => new { product.Name, product.Type, product.Price, product.StockQuantity, product.IsActive, product.IsDeleted, product.CreatedAt, product.UpdatedAt })
                .SingleAsync();

            Assert.Equal((before.Name, before.Type, before.Price, before.StockQuantity, before.IsDeleted, before.CreatedAt),
                (after.Name, after.Type, after.Price, after.StockQuantity, after.IsDeleted, after.CreatedAt));
            Assert.True(before.IsActive);
            Assert.False(after.IsActive);
            Assert.InRange(Math.Abs((after.UpdatedAt!.Value - result.UpdatedAt).Ticks), 0, 10);
            Assert.Equal(fixture.ProductId, result.Id);
            Assert.False(result.IsActive);
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((0, 0, 1, 1, 1), fixture.CleanupCounts);
        }
    }
}
