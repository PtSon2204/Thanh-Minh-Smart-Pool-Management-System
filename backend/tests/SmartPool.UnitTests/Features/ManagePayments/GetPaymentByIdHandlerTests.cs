using Moq;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManagePayments;

public sealed class GetPaymentByIdHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Tìm thấy giao dịch -> Trả đúng PaymentDetailDto")]
    public async Task UTCID01_PaymentFound_ReturnsSameDetail()
    {
        var paymentId = Guid.NewGuid();
        var expected = new PaymentDetailDto
        {
            PaymentId = paymentId,
            OrderId = Guid.NewGuid(),
            InvoiceCode = "INV-12345678",
            Amount = 150_000m,
            PaymentMethod = "CASH",
            Status = "COMPLETED",
            TransactionRef = "TXN-123",
            CustomerName = "Nguyen Van A",
            Items = [new() { ItemName = "Vé lượt", Quantity = 1, UnitPrice = 150_000m }]
        };
        var operations = new Mock<IPaymentOperations>();
        using var cts = new CancellationTokenSource();
        operations.Setup(o => o.GetPaymentByIdAsync(paymentId, cts.Token)).ReturnsAsync(expected);

        var result = await new GetPaymentByIdHandler(operations.Object)
            .Handle(new GetPaymentByIdQuery { PaymentId = paymentId }, cts.Token);

        Assert.Same(expected, result);
        Assert.Equal(paymentId, result.PaymentId);
        Assert.Equal(expected.OrderId, result.OrderId);
        Assert.Equal(expected.InvoiceCode, result.InvoiceCode);
        Assert.Equal(expected.Amount, result.Amount);
        Assert.Equal(expected.PaymentMethod, result.PaymentMethod);
        Assert.Equal(expected.Status, result.Status);
        Assert.Equal(expected.TransactionRef, result.TransactionRef);
        Assert.Equal("Vé lượt", Assert.Single(result.Items).ItemName);
        operations.Verify(o => o.GetPaymentByIdAsync(paymentId, cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Không tìm thấy giao dịch -> Ném KeyNotFoundException đúng mã")]
    public async Task UTCID02_PaymentMissing_ThrowsKeyNotFound()
    {
        var paymentId = Guid.NewGuid();
        var operations = new Mock<IPaymentOperations>();
        operations.Setup(o => o.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDetailDto?)null);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetPaymentByIdHandler(operations.Object)
                .Handle(new GetPaymentByIdQuery { PaymentId = paymentId }, default));

        Assert.Equal($"Không tìm thấy giao dịch thanh toán với mã: {paymentId}", exception.Message);
        operations.Verify(o => o.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - IPaymentOperations ném exception -> Handler truyền tiếp exception")]
    public async Task UTCID03_OperationsFailure_IsPropagated()
    {
        var paymentId = Guid.NewGuid();
        var operations = new Mock<IPaymentOperations>();
        var failure = new InvalidOperationException("Payment detail read failed");
        operations.Setup(o => o.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetPaymentByIdHandler(operations.Object)
                .Handle(new GetPaymentByIdQuery { PaymentId = paymentId }, default));

        Assert.Same(failure, actual);
        operations.Verify(o => o.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
