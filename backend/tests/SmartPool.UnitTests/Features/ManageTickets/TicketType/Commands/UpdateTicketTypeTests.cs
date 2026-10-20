using System.Linq.Expressions;
using Moq;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Commands;

public class UpdateTicketTypeTests
{
    [Fact(DisplayName = "UTCID01 (N) - Cập nhật hợp lệ -> Lưu đúng dữ liệu và thời điểm cập nhật")]
    public async Task Handle_ValidRequest_UpdatesEntityAndResponse()
    {
        var entity = TicketTypeTestSupport.Entity(active: false);
        var createdAt = entity.CreatedAt;
        var repo = TicketTypeTestSupport.Repository(entity);
        repo.Setup(r => r.Update(entity));
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        uow.Setup(u => u.SaveChangesAsync(cts.Token)).ReturnsAsync(1);
        var command = ValidCommand();
        command.Id = entity.Id;
        var before = DateTime.UtcNow;

        var result = await new UpdateTicketTypeHandler(repo.Object, uow.Object, TicketTypeTestSupport.Mapper())
            .Handle(command, cts.Token);

        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(command.Name, entity.Name);
        Assert.Equal("VE_LUOT", entity.TicketCategory);
        Assert.Equal(command.Price, entity.Price);
        Assert.Equal(command.DurationDays, entity.DurationDays);
        Assert.InRange(entity.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(createdAt, entity.CreatedAt);
        Assert.False(entity.IsActive);
        Assert.False(entity.IsDeleted);
        Assert.Equal(command.Name, result.Name);
        Assert.Equal(command.TicketCategory, result.TicketCategory);
        Assert.Equal(command.Price, result.Price);
        Assert.Equal(command.DurationDays, result.DurationDays);
        Assert.Equal(entity.UpdatedAt, result.UpdatedAt);
        Assert.False(result.IsActive);
        repo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketTypeEntity, bool>>>(), cts.Token), Times.Once);
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Hiệu lực null -> Tự đặt vé lượt 1 ngày và giữ trạng thái mặc định hoạt động")]
    public async Task Handle_NullDuration_SetsFixedDuration()
    {
        var entity = TicketTypeTestSupport.Entity(active: null, deleted: null);
        var repo = TicketTypeTestSupport.Repository(entity);
        repo.Setup(r => r.Update(entity));
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var command = ValidCommand();
        command.Id = entity.Id;
        command.DurationDays = null;
        var result = await new UpdateTicketTypeHandler(repo.Object, uow.Object, TicketTypeTestSupport.Mapper())
            .Handle(command, CancellationToken.None);
        Assert.Equal(1, entity.DurationDays);
        Assert.Equal(1, result.DurationDays);
        Assert.True(result.IsActive);
    }

    [Fact(DisplayName = "UTCID03 (A) - Id không tồn tại -> Báo không tìm thấy và không lưu")]
    public Task Handle_MissingId_Throws() => AssertNotFound(TicketTypeTestSupport.Entity(), Guid.NewGuid());

    [Fact(DisplayName = "UTCID04 (A) - Vé đã xóa mềm -> Báo không tìm thấy và không lưu")]
    public Task Handle_DeletedTicket_Throws()
    {
        var entity = TicketTypeTestSupport.Entity(deleted: true);
        return AssertNotFound(entity, entity.Id);
    }

    private static async Task AssertNotFound(TicketTypeEntity entity, Guid id)
    {
        var repo = TicketTypeTestSupport.Repository(entity);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var command = ValidCommand();
        command.Id = id;
        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateTicketTypeHandler(repo.Object, uow.Object, TicketTypeTestSupport.Mapper())
                .Handle(command, CancellationToken.None));
        Assert.Equal($"Không tìm thấy loại vé với Id: {id}", error.Message);
        repo.Verify(r => r.Update(It.IsAny<TicketTypeEntity>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (A) - Lỗi đọc repository -> Truyền lỗi và không cập nhật hoặc lưu")]
    public async Task Handle_ReadFailure_DoesNotSave()
    {
        var failure = new InvalidOperationException("Read failed");
        var repo = TicketTypeTestSupport.Repository();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketTypeEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateTicketTypeHandler(repo.Object, uow.Object, TicketTypeTestSupport.Mapper())
                .Handle(ValidCommand(), CancellationToken.None)));
        repo.Verify(r => r.Update(It.IsAny<TicketTypeEntity>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - Lỗi lưu thay đổi -> Truyền lỗi và không trả response thành công")]
    public async Task Handle_SaveFailure_PropagatesException()
    {
        var entity = TicketTypeTestSupport.Entity();
        var repo = TicketTypeTestSupport.Repository(entity);
        repo.Setup(r => r.Update(entity));
        var uow = new Mock<IUnitOfWork>();
        var failure = new InvalidOperationException("Save failed");
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var command = ValidCommand();
        command.Id = entity.Id;
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateTicketTypeHandler(repo.Object, uow.Object, TicketTypeTestSupport.Mapper())
                .Handle(command, CancellationToken.None)));
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID07 (N) - Tên tiếng Việt có dấu và số -> Hợp lệ")]
    public void Validate_VietnameseName()
    {
        var command = ValidCommand();
        command.Name = "Vé tháng 30 ngày";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID08 (N) - Tên tiếng Việt dùng dấu Unicode tổ hợp -> Hợp lệ")]
    public void Validate_DecomposedVietnameseName()
    {
        var command = ValidCommand();
        command.Name = "Ve\u0301 lu\u031bo\u0323t 1";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID09 (B) - Tên đúng 30 ký tự -> Hợp lệ")]
    public void Validate_NameAtLimit()
    {
        var command = ValidCommand();
        command.Name = new string('A', 30);

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID10 (B) - Tên 31 ký tự -> Báo lỗi Name")]
    public void Validate_NameOverLimit()
    {
        var command = ValidCommand();
        command.Name = new string('A', 31);

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID11 (N) - Tên chứa dấu ngoặc -> Hợp lệ")]
    public void Validate_NameWithParentheses()
    {
        var command = ValidCommand();
        command.Name = "Vé tháng (30 ngày)";

        var result = new UpdateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID12 (A) - Tên chứa ký tự đặc biệt -> Báo lỗi Name")]
    public void Validate_NameWithSpecialCharacters()
    {
        var command = ValidCommand();
        command.Name = "/*/--*";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID13 (A) - Tên chứa dấu gạch dưới -> Báo lỗi Name")]
    public void Validate_NameWithUnderscore()
    {
        var command = ValidCommand();
        command.Name = "Vé_123";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID14 (A) - Tên chứa emoji -> Báo lỗi Name")]
    public void Validate_NameWithEmoji()
    {
        var command = ValidCommand();
        command.Name = "Vé 🎟";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID15 (A) - Tên chứa ký tự xuống dòng -> Báo lỗi Name")]
    public void Validate_NameWithNewline()
    {
        var command = ValidCommand();
        command.Name = "Vé\n";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID16 (A) - Tên chứa tab -> Báo lỗi Name")]
    public void Validate_NameWithTab()
    {
        var command = ValidCommand();
        command.Name = "Vé\ttháng";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID17 (B) - Tên rỗng -> Báo lỗi Name")]
    public void Validate_EmptyName()
    {
        var command = ValidCommand();
        command.Name = "";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID18 (A) - Tên chỉ có khoảng trắng -> Báo lỗi Name")]
    public void Validate_WhitespaceName()
    {
        var command = ValidCommand();
        command.Name = "   ";

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID19 (A) - Tên null -> Báo lỗi Name")]
    public void Validate_NullName()
    {
        var command = ValidCommand();
        command.Name = null!;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }

    [Fact(DisplayName = "UTCID20 (N) - Phân loại VE_THANG -> Hợp lệ")]
    public void Validate_MonthlyCategory()
    {
        var command = ValidCommand();
        command.TicketCategory = TicketCategoryEnum.VE_THANG;
        command.DurationDays = 30;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID21 (N) - Phân loại VE_LUOT -> Hợp lệ")]
    public void Validate_SingleUseCategory()
    {
        var command = ValidCommand();
        command.TicketCategory = TicketCategoryEnum.VE_LUOT;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID22 (A) - Phân loại không tồn tại -> Báo lỗi TicketCategory")]
    public void Validate_InvalidCategory()
    {
        var command = ValidCommand();
        command.TicketCategory = (TicketCategoryEnum)2;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "TicketCategory");
    }

    [Fact(DisplayName = "UTCID23 (A) - Giá vé âm -> Báo lỗi Price")]
    public void Validate_NegativePrice()
    {
        var command = ValidCommand();
        command.Price = -50000;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Price");
    }

    [Fact(DisplayName = "UTCID24 (B) - Giá vé bằng 0 -> Báo lỗi Price")]
    public void Validate_ZeroPrice()
    {
        var command = ValidCommand();
        command.Price = 0;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Price");
    }

    [Fact(DisplayName = "UTCID25 (B) - Giá vé bằng 1 -> Hợp lệ")]
    public void Validate_MinimumPrice()
    {
        var command = ValidCommand();
        command.Price = 1;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID26 (N) - Không gửi hiệu lực -> Hợp lệ và được tự xác định theo loại vé")]
    public void Validate_OmittedDuration()
    {
        var command = ValidCommand();
        command.DurationDays = null;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID27 (B) - Hiệu lực 1 ngày -> Hợp lệ")]
    public void Validate_MinimumDuration()
    {
        var command = ValidCommand();
        command.DurationDays = 1;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID28 (B) - Hiệu lực bằng 0 -> Báo lỗi DurationDays")]
    public void Validate_ZeroDuration()
    {
        var command = ValidCommand();
        command.DurationDays = 0;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
    }

    [Fact(DisplayName = "UTCID29 (A) - Hiệu lực âm -> Báo lỗi DurationDays")]
    public void Validate_NegativeDuration()
    {
        var command = ValidCommand();
        command.DurationDays = -1;

        var result = new UpdateTicketTypeValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
    }

    [Fact(DisplayName = "UTCID30 (B) - Id rỗng -> Báo lỗi Id")]
    public void Validate_EmptyId_ReturnsError()
    {
        var command = ValidCommand();
        command.Id = Guid.Empty;
        var result = new UpdateTicketTypeValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Id");
    }

    [Fact(DisplayName = "UTCID31 (A) - Vé lượt gửi 30 ngày -> Báo lỗi DurationDays")]
    public void Validate_SingleUseWithMonthlyDuration_ReturnsError()
    {
        var command = ValidCommand();
        command.DurationDays = 30;
        var result = new UpdateTicketTypeValidator().Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
    }

    [Fact(DisplayName = "UTCID32 (A) - Vé tháng gửi 1 ngày -> Báo lỗi DurationDays")]
    public void Validate_MonthlyWithSingleUseDuration_ReturnsError()
    {
        var command = ValidCommand();
        command.TicketCategory = TicketCategoryEnum.VE_THANG;
        var result = new UpdateTicketTypeValidator().Validate(command);
        Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
    }

    [Theory(DisplayName = "UTCID33 (N) - Đổi phân loại vé -> Server tự thay hiệu lực cũ bằng giá trị cố định")]
    [InlineData(TicketCategoryEnum.VE_THANG, null, 30)]
    [InlineData(TicketCategoryEnum.VE_LUOT, null, 1)]
    [InlineData(TicketCategoryEnum.VE_THANG, 1, 30)]
    [InlineData(TicketCategoryEnum.VE_LUOT, 30, 1)]
    public async Task Handle_DerivesDurationFromCategory(TicketCategoryEnum category, int? suppliedDays, int expectedDays)
    {
        var entity = TicketTypeTestSupport.Entity();
        var repo = TicketTypeTestSupport.Repository(entity);
        repo.Setup(r => r.Update(entity));
        var command = ValidCommand();
        command.Id = entity.Id;
        command.TicketCategory = category;
        command.DurationDays = suppliedDays;
        var result = await new UpdateTicketTypeHandler(repo.Object, new Mock<IUnitOfWork>().Object, TicketTypeTestSupport.Mapper())
            .Handle(command, CancellationToken.None);
        Assert.Equal(expectedDays, entity.DurationDays);
        Assert.Equal(expectedDays, result.DurationDays);
    }

    private static UpdateTicketTypeCommand ValidCommand() => new()
    {
        Id = Guid.NewGuid(), Name = "Vé lượt mới", TicketCategory = TicketCategoryEnum.VE_LUOT,
        Price = 50000, DurationDays = 1
    };
}
