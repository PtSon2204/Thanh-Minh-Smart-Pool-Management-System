using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Moq;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class GetEquipmentsTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPagination_DoesNotReachDatabase(int pageIndex, int pageSize)
    {
        var repository = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var handler = new GetEquipmentsHandler(repository.Object, new GetEquipmentsValidator(), TimeProvider.System);
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetEquipmentsQuery
        {
            PageIndex = pageIndex, PageSize = pageSize
        }, CancellationToken.None));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handler_UsesVietnamDateAndPreservesCombinedFilters()
    {
        var repository = new Mock<IEquipmentOperations>();
        var request = new GetEquipmentsQuery
        {
            SearchTerm = "pump", Category = "filter", Location = "pool", Status = "Active", PageIndex = 2, PageSize = 20
        };
        var expected = new PagedResponse<EquipmentListItem> { PageIndex = 2, PageSize = 20 };
        var cancellationToken = new CancellationTokenSource().Token;
        repository.Setup(x => x.GetListAsync(request, new DateOnly(2026, 10, 16), cancellationToken)).ReturnsAsync(expected);
        var handler = new GetEquipmentsHandler(repository.Object, new GetEquipmentsValidator(), new FixedTime());
        Assert.Same(expected, await handler.Handle(request, cancellationToken));
        repository.VerifyAll();
    }

    [Fact]
    public void Validator_RejectsOversizedSearchAndFilters()
    {
        var result = new GetEquipmentsValidator().Validate(new GetEquipmentsQuery
        {
            SearchTerm = new string('x', 256), Category = new string('x', 51),
            Location = new string('x', 256), Status = new string('x', 51)
        });
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void AllEquipmentEndpoints_RequireExistingAdminPolicy()
    {
        var attribute = Assert.Single(typeof(EquipmentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.AdminOnly, attribute.Policy);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);
    }
}
