using Moq;
using SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode;
using SmartPool.Application.Interfaces.Repositories;
using TicketEntity = SmartPool.Domain.Entities.Ticket;

namespace SmartPool.UnitTests.Features.ManageTickets.Ticket.Commands;

public sealed class UpdateTicketCodeTests
{
    [Fact(DisplayName = "UTCID01 (N) - Vé ACTIVE thuộc người dùng, mã duy nhất -> Cập nhật và lưu thành công")]
    public async Task UTCID01_ValidUpdate_ChangesCodeAndSaves()
    {
        var ownerId = Guid.NewGuid();
        var ticket = MakeTicket(ownerId);
        var oldUpdatedAt = new DateTime(2025, 1, 1);
        ticket.UpdatedAt = oldUpdatedAt;
        var operations = new Mock<ITicketOrderOperations>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        operations.Setup(r => r.GetTicketByIdAsync(ticket.Id, cts.Token)).ReturnsAsync(ticket);
        operations.Setup(r => r.TicketCodeExistsAsync("012345", ticket.Id, cts.Token)).ReturnsAsync(false);
        operations.Setup(r => r.UpdateTicketAsync(ticket, cts.Token)).Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;

        var result = await new UpdateTicketCodeHandler(operations.Object)
            .Handle(new UpdateTicketCodeCommand { TicketId = ticket.Id, UserId = ownerId, Code = "012345" }, cts.Token);

        Assert.True(result.Success);
        Assert.Equal("Cập nhật mã vé thành công.", result.Message);
        Assert.Equal("012345", result.NewCode);
        Assert.Equal("012345", ticket.QrCode);
        Assert.NotEqual(oldUpdatedAt, ticket.UpdatedAt);
        Assert.InRange(ticket.UpdatedAt!.Value, before, DateTime.UtcNow);
        operations.Verify(r => r.GetTicketByIdAsync(ticket.Id, cts.Token), Times.Once);
        operations.Verify(r => r.TicketCodeExistsAsync("012345", ticket.Id, cts.Token), Times.Once);
        operations.Verify(r => r.UpdateTicketAsync(ticket, cts.Token), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - TicketId không tồn tại -> Trả lỗi và không truy vấn trùng/lưu")]
    public async Task UTCID02_TicketMissing_ReturnsFailureWithoutSaving()
    {
        var ticketId = Guid.NewGuid();
        var operations = new Mock<ITicketOrderOperations>(MockBehavior.Strict);
        operations.Setup(r => r.GetTicketByIdAsync(ticketId, CancellationToken.None)).ReturnsAsync((TicketEntity?)null);

        var result = await new UpdateTicketCodeHandler(operations.Object)
            .Handle(new UpdateTicketCodeCommand { TicketId = ticketId, UserId = Guid.NewGuid(), Code = "123456" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Không tìm thấy vé.", result.Message);
        Assert.Null(result.NewCode);
        operations.Verify(r => r.GetTicketByIdAsync(ticketId, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Người dùng không phải chủ vé -> Từ chối và không lưu")]
    public async Task UTCID03_WrongOwner_ReturnsFailureWithoutSaving()
    {
        var ticket = MakeTicket(Guid.NewGuid());
        var operations = new Mock<ITicketOrderOperations>(MockBehavior.Strict);
        operations.Setup(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None)).ReturnsAsync(ticket);

        var result = await new UpdateTicketCodeHandler(operations.Object)
            .Handle(new UpdateTicketCodeCommand { TicketId = ticket.Id, UserId = Guid.NewGuid(), Code = "123456" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bạn không có quyền chỉnh sửa vé này.", result.Message);
        Assert.Equal("OLD123", ticket.QrCode);
        operations.Verify(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Vé không ở trạng thái ACTIVE -> Từ chối cập nhật")]
    public async Task UTCID04_InactiveTicket_ReturnsFailureWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var ticket = MakeTicket(ownerId);
        ticket.Status = "EXPIRED";
        var operations = new Mock<ITicketOrderOperations>(MockBehavior.Strict);
        operations.Setup(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None)).ReturnsAsync(ticket);

        var result = await new UpdateTicketCodeHandler(operations.Object)
            .Handle(new UpdateTicketCodeCommand { TicketId = ticket.Id, UserId = ownerId, Code = "123456" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Chỉ có thể chỉnh sửa mã khi vé đang ở trạng thái ACTIVE.", result.Message);
        Assert.Equal("OLD123", ticket.QrCode);
        operations.Verify(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Mã mới đã được vé khác sử dụng -> Từ chối và không lưu")]
    public async Task UTCID05_DuplicateCode_ReturnsFailureWithoutMutation()
    {
        var ownerId = Guid.NewGuid();
        var ticket = MakeTicket(ownerId);
        var operations = new Mock<ITicketOrderOperations>(MockBehavior.Strict);
        operations.Setup(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None)).ReturnsAsync(ticket);
        operations.Setup(r => r.TicketCodeExistsAsync("123456", ticket.Id, CancellationToken.None))
            .ReturnsAsync(true);

        var result = await new UpdateTicketCodeHandler(operations.Object)
            .Handle(new UpdateTicketCodeCommand { TicketId = ticket.Id, UserId = ownerId, Code = "123456" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Mã này đã được dùng bởi vé khác. Vui lòng chọn mã khác.", result.Message);
        Assert.Equal("OLD123", ticket.QrCode);
        operations.Verify(r => r.GetTicketByIdAsync(ticket.Id, CancellationToken.None), Times.Once);
        operations.Verify(r => r.TicketCodeExistsAsync("123456", ticket.Id, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (N) - Code đúng 6 chữ số -> Validator chấp nhận")]
    public void UTCID06_ValidSixDigitCode_PassesValidation()
    {
        var result = new UpdateTicketCodeValidator().Validate(new UpdateTicketCodeCommand { Code = "012345" });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID07 (A) - Code rỗng -> Validator báo không được để trống")]
    public void UTCID07_EmptyCode_FailsValidation()
    {
        var result = new UpdateTicketCodeValidator().Validate(new UpdateTicketCodeCommand { Code = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateTicketCodeCommand.Code)
            && error.ErrorMessage == "Mã vé không được để trống.");
    }

    [Fact(DisplayName = "UTCID08 (A) - Code ngắn hơn 3 ký tự -> Validator báo sai độ dài")]
    public void UTCID08_ShortCode_FailsValidation()
    {
        var result = new UpdateTicketCodeValidator().Validate(new UpdateTicketCodeCommand { Code = "12" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateTicketCodeCommand.Code)
            && error.ErrorMessage == "Mã vé phải có độ dài từ 3 đến 6 ký tự.");
    }

    [Fact(DisplayName = "UTCID09 (A) - Code dài hơn 6 ký tự -> Validator báo sai độ dài")]
    public void UTCID09_LongCode_FailsValidation()
    {
        var result = new UpdateTicketCodeValidator().Validate(new UpdateTicketCodeCommand { Code = "1234567" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateTicketCodeCommand.Code)
            && error.ErrorMessage == "Mã vé phải có độ dài từ 3 đến 6 ký tự.");
    }

    [Fact(DisplayName = "UTCID10 (A) - Code có ký tự không phải chữ số -> Validator từ chối")]
    public void UTCID10_NonDigitCode_FailsValidation()
    {
        var result = new UpdateTicketCodeValidator().Validate(new UpdateTicketCodeCommand { Code = "12A456" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateTicketCodeCommand.Code)
            && error.ErrorMessage == "Mã vé chỉ được chứa từ 3 đến 6 chữ số (0–9).");
    }

    private static TicketEntity MakeTicket(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = ownerId,
        QrCode = "OLD123",
        Status = "ACTIVE",
        UpdatedAt = new DateTime(2025, 1, 1)
    };
}
