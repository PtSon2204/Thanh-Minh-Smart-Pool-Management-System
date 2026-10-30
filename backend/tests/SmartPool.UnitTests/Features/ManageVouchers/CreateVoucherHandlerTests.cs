using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageVouchers;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageVouchers;

public class CreateVoucherHandlerTests
{
    private readonly Mock<IVoucherOperations> operations = new();

    private CreateVoucherHandler CreateHandler() => new(operations.Object, new CreateVoucherValidator());

    [Fact(DisplayName = "UTCID01 (B) - Code rỗng -> ValidationException và không gọi operation")]
    public async Task UTCID01_EmptyCode_IsRejectedBeforeOperation()
    {
        var command = ValidCommand();
        command.Code = string.Empty;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.Code));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (A) - DiscountType không thuộc 2 giá trị cho phép -> ValidationException")]
    public async Task UTCID02_UnsupportedDiscountType_IsRejected()
    {
        var command = ValidCommand();
        command.DiscountType = "percentage";

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.DiscountType));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID03 (B) - DiscountValue bằng 0 -> ValidationException")]
    public async Task UTCID03_ZeroDiscountValue_IsRejected()
    {
        var command = ValidCommand();
        command.DiscountValue = 0;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.DiscountValue));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (A) - DiscountValue âm -> ValidationException")]
    public async Task UTCID04_NegativeDiscountValue_IsRejected()
    {
        var command = ValidCommand();
        command.DiscountValue = -1;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.DiscountValue));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (A) - MinOrderValue âm -> ValidationException")]
    public async Task UTCID05_NegativeMinOrderValue_IsRejected()
    {
        var command = ValidCommand();
        command.MinOrderValue = -1;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.MinOrderValue));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - EndDate bằng StartDate -> ValidationException")]
    public async Task UTCID06_EqualStartAndEndDates_AreRejected()
    {
        var command = ValidCommand();
        command.StartDate = new DateTime(2026, 10, 1);
        command.EndDate = command.StartDate;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.EndDate));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (A) - EndDate trước StartDate -> ValidationException")]
    public async Task UTCID07_EndDateBeforeStartDate_IsRejected()
    {
        var command = ValidCommand();
        command.StartDate = new DateTime(2026, 10, 2);
        command.EndDate = new DateTime(2026, 10, 1);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().Handle(command, default));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(command.EndDate));
        operations.Verify(x => x.CreateVoucherAsync(It.IsAny<CreateVoucherCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (N) - PERCENTAGE hợp lệ, ngày và MinOrderValue null -> Trả response operation")]
    public async Task UTCID08_ValidPercentageWithoutOptionalValues_IsForwarded()
    {
        var command = ValidCommand();
        command.DiscountType = "PERCENTAGE";
        command.DiscountValue = 15;
        command.MinOrderValue = null;
        command.StartDate = null;
        command.EndDate = null;
        command.IsActive = null;
        var expected = new CreateVoucherResponse { Id = Guid.NewGuid(), Code = command.Code, Message = "operation result" };
        operations.Setup(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await CreateHandler().Handle(command, default);

        Assert.Same(expected, result);
        operations.Verify(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (N) - FIXED_AMOUNT hợp lệ, MinOrderValue = 0, EndDate sau StartDate -> Gọi operation đúng 1 lần")]
    public async Task UTCID09_ValidFixedAmount_IsForwarded()
    {
        var command = ValidCommand();
        command.DiscountType = "FIXED_AMOUNT";
        command.DiscountValue = 5000;
        command.MinOrderValue = 0;
        command.StartDate = DateTime.UtcNow.Date.AddDays(1);
        command.EndDate = DateTime.UtcNow.Date.AddDays(2);
        command.IsActive = false;
        operations.Setup(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateVoucherResponse { Id = Guid.NewGuid(), Code = command.Code, Message = "Tạo voucher thành công." });

        var result = await CreateHandler().Handle(command, default);

        Assert.Equal(command.Code, result.Code);
        operations.Verify(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (A) - Operation ném VoucherConflictException -> Handler truyền tiếp exception")]
    public async Task UTCID10_OperationConflict_IsPropagated()
    {
        var command = ValidCommand();
        var expected = new VoucherConflictException("Voucher with code 'WELCOME' already exists.");
        operations.Setup(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>())).ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<VoucherConflictException>(() => CreateHandler().Handle(command, default));

        Assert.Same(expected, actual);
        operations.Verify(x => x.CreateVoucherAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreateVoucherCommand ValidCommand() => new()
    {
        Code = "WELCOME",
        DiscountType = "PERCENTAGE",
        DiscountValue = 10
    };
}
