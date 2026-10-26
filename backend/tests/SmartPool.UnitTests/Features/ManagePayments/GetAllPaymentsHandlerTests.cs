using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments;
using SmartPool.Application.Interfaces.Repositories;
using Moq;

namespace SmartPool.UnitTests.Features.ManagePayments;

public sealed class GetAllPaymentsHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Query mặc định -> Chuyển nguyên query và trả PagedResponse")]
    public async Task UTCID01_DefaultQuery_DelegatesAndReturnsResponse()
    {
        var operations = new Mock<IPaymentOperations>();
        var query = new GetAllPaymentsQuery();
        var expected = new PagedResponse<PaymentSummaryDto> { PageIndex = 1, PageSize = 20, TotalCount = 1 };
        expected.Items.Add(new PaymentSummaryDto { PaymentId = Guid.NewGuid(), TransactionRef = "SP123456" });
        using var cts = new CancellationTokenSource();
        operations.Setup(o => o.GetAllPaymentsAsync(query, cts.Token)).ReturnsAsync(expected);

        var result = await new GetAllPaymentsHandler(operations.Object).Handle(query, cts.Token);

        Assert.Same(expected, result);
        operations.Verify(o => o.GetAllPaymentsAsync(query, cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Query có search, filter ngày, status, payment method và pagination -> Chuyển nguyên giá trị")]
    public async Task UTCID02_FilteredQuery_ForwardsAllValuesUnchanged()
    {
        var operations = new Mock<IPaymentOperations>();
        var query = new GetAllPaymentsQuery
        {
            PageIndex = 3,
            PageSize = 10,
            Status = "COMPLETED",
            PaymentMethod = "TRANSFER",
            FromDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 10, 24, 23, 59, 59, DateTimeKind.Utc),
            Search = "SP123456"
        };
        var expected = new PagedResponse<PaymentSummaryDto> { PageIndex = 3, PageSize = 10, TotalCount = 21 };
        operations.Setup(o => o.GetAllPaymentsAsync(query, CancellationToken.None)).ReturnsAsync(expected);

        var result = await new GetAllPaymentsHandler(operations.Object).Handle(query, CancellationToken.None);

        Assert.Same(expected, result);
        operations.Verify(o => o.GetAllPaymentsAsync(
            It.Is<GetAllPaymentsQuery>(q => q.PageIndex == 3 && q.PageSize == 10 && q.Status == "COMPLETED"
                && q.PaymentMethod == "TRANSFER" && q.FromDate == query.FromDate && q.ToDate == query.ToDate
                && q.Search == "SP123456"), CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (B) - Operations trả trang rỗng -> Handler trả đúng trang rỗng")]
    public async Task UTCID03_EmptyPage_PreservesPagedResponse()
    {
        var operations = new Mock<IPaymentOperations>();
        var query = new GetAllPaymentsQuery { PageIndex = 2, PageSize = 5, Search = "not-found" };
        var expected = new PagedResponse<PaymentSummaryDto> { PageIndex = 2, PageSize = 5, TotalCount = 0 };
        operations.Setup(o => o.GetAllPaymentsAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new GetAllPaymentsHandler(operations.Object).Handle(query, default);

        Assert.Same(expected, result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(5, result.PageSize);
    }

    [Fact(DisplayName = "UTCID04 (A) - IPaymentOperations ném exception -> Handler truyền tiếp exception")]
    public async Task UTCID04_OperationsFailure_IsPropagated()
    {
        var operations = new Mock<IPaymentOperations>();
        var query = new GetAllPaymentsQuery();
        var failure = new InvalidOperationException("Payment history read failed");
        operations.Setup(o => o.GetAllPaymentsAsync(query, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetAllPaymentsHandler(operations.Object).Handle(query, default));

        Assert.Same(failure, actual);
        operations.Verify(o => o.GetAllPaymentsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }
}
