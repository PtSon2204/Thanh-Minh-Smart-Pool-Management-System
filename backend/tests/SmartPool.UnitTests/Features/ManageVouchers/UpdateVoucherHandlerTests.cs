using System.Linq.Expressions;
using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Features.ManageVouchers;
using SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher;
using SmartPool.Application.Features.ManageVouchers.Mapping;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageVouchers;

public class UpdateVoucherHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Cập nhật hợp lệ -> Chuẩn hóa Code, cập nhật dữ liệu, map response và lưu")]
    public async Task UTCID01_ValidUpdate_NormalizesCodeUpdatesAndSaves()
    {
        var entity = ExistingVoucher();
        var createdAt = entity.CreatedAt;
        var (repo, unitOfWork) = Dependencies(entity);
        var command = ValidCommand(entity.Id);
        command.Code = "  summer 10  ";
        command.DiscountType = "FIXED_AMOUNT";
        command.DiscountValue = 5000;
        command.MinOrderValue = 10000;
        command.StartDate = new DateTime(2026, 10, 1);
        command.EndDate = new DateTime(2026, 11, 1);
        command.IsActive = false;
        using var cts = new CancellationTokenSource();

        var result = await Handler(repo.Object, unitOfWork.Object).Handle(command, cts.Token);

        Assert.Equal("SUMMER 10", entity.Code);
        Assert.Equal(command.DiscountType, entity.DiscountType);
        Assert.Equal(command.DiscountValue, entity.DiscountValue);
        Assert.Equal(command.MinOrderValue, entity.MinOrderValue);
        Assert.Equal(command.StartDate, entity.StartDate);
        Assert.Equal(command.EndDate, entity.EndDate);
        Assert.False(entity.IsActive);
        Assert.Equal(createdAt, entity.CreatedAt);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.Code, result.Code);
        Assert.Equal(entity.DiscountType, result.DiscountType);
        Assert.Equal(entity.DiscountValue, result.DiscountValue);
        Assert.Equal(entity.MinOrderValue, result.MinOrderValue);
        Assert.Equal(entity.StartDate, result.StartDate);
        Assert.Equal(entity.EndDate, result.EndDate);
        Assert.Equal(entity.IsActive, result.IsActive);
        Assert.Equal(createdAt, result.CreatedAt);
        repo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), cts.Token), Times.Exactly(2));
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Giữ nguyên Code của chính voucher -> Không xem chính nó là mã trùng")]
    public async Task UTCID02_UnchangedCode_DoesNotConflictWithSameEntity()
    {
        var entity = ExistingVoucher();
        var (repo, unitOfWork) = Dependencies(entity);
        var command = ValidCommand(entity.Id);
        command.Code = entity.Code;

        var result = await Handler(repo.Object, unitOfWork.Object).Handle(command, default);

        Assert.Equal(entity.Code, result.Code);
        repo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - IsActive = null -> Gán null vào entity và response")]
    public async Task UTCID03_NullIsActive_IsAssigned()
    {
        var entity = ExistingVoucher(active: true);
        var (repo, unitOfWork) = Dependencies(entity);
        var command = ValidCommand(entity.Id);
        command.IsActive = null;

        var result = await Handler(repo.Object, unitOfWork.Object).Handle(command, default);

        Assert.Null(entity.IsActive);
        Assert.Null(result.IsActive);
    }

    [Fact(DisplayName = "UTCID04 (A) - Id không tồn tại -> KeyNotFoundException và không cập nhật hoặc lưu")]
    public async Task UTCID04_MissingId_ThrowsNotFound()
    {
        var (repo, unitOfWork) = Dependencies();
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Handler(repo.Object, unitOfWork.Object).Handle(ValidCommand(id), default));

        Assert.Equal($"Không tìm thấy voucher với Id: {id}", exception.Message);
        repo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.Update(It.IsAny<Voucher>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (A) - Voucher hiện tại đã xóa mềm -> Không tìm thấy và không lưu")]
    public async Task UTCID05_DeletedVoucher_ThrowsNotFound()
    {
        var entity = ExistingVoucher(deleted: true);
        var (repo, unitOfWork) = Dependencies(entity);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Handler(repo.Object, unitOfWork.Object)
            .Handle(ValidCommand(entity.Id), default));

        repo.Verify(r => r.Update(It.IsAny<Voucher>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - Code thuộc voucher khác chưa xóa -> VoucherConflictException và không lưu")]
    public async Task UTCID06_DuplicateCode_ThrowsConflict()
    {
        var entity = ExistingVoucher();
        var duplicate = ExistingVoucher(code: "NEWCODE");
        var (repo, unitOfWork) = Dependencies(entity, duplicate);
        var command = ValidCommand(entity.Id);
        command.Code = "NEWCODE";

        var exception = await Assert.ThrowsAsync<VoucherConflictException>(() =>
            Handler(repo.Object, unitOfWork.Object).Handle(command, default));

        Assert.Equal("Mã voucher 'NEWCODE' đã tồn tại.", exception.Message);
        repo.Verify(r => r.Update(It.IsAny<Voucher>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID07 (N) - Code trùng với voucher đã xóa mềm -> Bỏ qua conflict và cập nhật")]
    public async Task UTCID07_DeletedVoucherWithSameCode_IsIgnoredForConflict()
    {
        var entity = ExistingVoucher();
        var deletedDuplicate = ExistingVoucher(code: "NEWCODE", deleted: true);
        var (repo, unitOfWork) = Dependencies(entity, deletedDuplicate);
        var command = ValidCommand(entity.Id);
        command.Code = "NEWCODE";

        var result = await Handler(repo.Object, unitOfWork.Object).Handle(command, default);

        Assert.Equal("NEWCODE", result.Code);
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID08 (A) - Repository lỗi khi đọc voucher hiện tại -> Truyền lỗi và không lưu")]
    public async Task UTCID08_FirstReadFailure_IsPropagated()
    {
        var repo = new Mock<IRepository<Voucher>>();
        var failure = new InvalidOperationException("Read failed");
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(repo.Object, unitOfWork.Object).Handle(ValidCommand(Guid.NewGuid()), default));

        Assert.Same(failure, actual);
        repo.Verify(r => r.Update(It.IsAny<Voucher>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 (A) - Repository lỗi khi kiểm tra Code trùng -> Truyền lỗi và không lưu")]
    public async Task UTCID09_ConflictLookupFailure_IsPropagated()
    {
        var entity = ExistingVoucher();
        var repo = new Mock<IRepository<Voucher>>();
        var failure = new InvalidOperationException("Conflict lookup failed");
        repo.SetupSequence(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity)
            .ThrowsAsync(failure);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(repo.Object, unitOfWork.Object).Handle(ValidCommand(entity.Id), default));

        Assert.Same(failure, actual);
        repo.Verify(r => r.Update(It.IsAny<Voucher>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 (A) - SaveChangesAsync lỗi -> Truyền lỗi sau khi gọi Update và Save")]
    public async Task UTCID10_SaveFailure_IsPropagated()
    {
        var entity = ExistingVoucher();
        var (repo, unitOfWork) = Dependencies(entity);
        var failure = new InvalidOperationException("Save failed");
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(repo.Object, unitOfWork.Object).Handle(ValidCommand(entity.Id), default));

        Assert.Same(failure, actual);
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 (N) - Validator: PERCENTAGE hợp lệ, các trường ngày và MinOrderValue null -> Hợp lệ")]
    public void UTCID11_Validator_AllowsValidPercentageAndNullOptionals()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.DiscountType = "PERCENTAGE";
        command.DiscountValue = 10;
        command.MinOrderValue = null;
        command.StartDate = null;
        command.EndDate = null;

        Assert.True(new UpdateVoucherValidator().Validate(command).IsValid);
    }

    [Fact(DisplayName = "UTCID12 (N) - Validator: FIXED_AMOUNT, MinOrderValue = 0 và EndDate sau StartDate -> Hợp lệ")]
    public void UTCID12_Validator_AllowsFixedAmountAndBoundaryValues()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.DiscountType = "FIXED_AMOUNT";
        command.DiscountValue = 1;
        command.MinOrderValue = 0;
        command.StartDate = new DateTime(2026, 10, 1);
        command.EndDate = new DateTime(2026, 10, 2);

        Assert.True(new UpdateVoucherValidator().Validate(command).IsValid);
    }

    [Fact(DisplayName = "UTCID13 (B) - Validator: Code rỗng -> Báo lỗi Code")]
    public void UTCID13_Validator_RejectsEmptyCode()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.Code = string.Empty;

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Code));
    }

    [Fact(DisplayName = "UTCID14 (A) - Validator: DiscountType không hợp lệ -> Báo lỗi DiscountType")]
    public void UTCID14_Validator_RejectsUnsupportedDiscountType()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.DiscountType = "percentage";

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DiscountType));
    }

    [Fact(DisplayName = "UTCID15 (B) - Validator: DiscountValue = 0 -> Báo lỗi DiscountValue")]
    public void UTCID15_Validator_RejectsZeroDiscountValue()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.DiscountValue = 0;

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DiscountValue));
    }

    [Fact(DisplayName = "UTCID16 (A) - Validator: DiscountValue âm -> Báo lỗi DiscountValue")]
    public void UTCID16_Validator_RejectsNegativeDiscountValue()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.DiscountValue = -1;

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.DiscountValue));
    }

    [Fact(DisplayName = "UTCID17 (A) - Validator: MinOrderValue âm -> Báo lỗi MinOrderValue")]
    public void UTCID17_Validator_RejectsNegativeMinOrderValue()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.MinOrderValue = -1;

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.MinOrderValue));
    }

    [Fact(DisplayName = "UTCID18 (B) - Validator: EndDate bằng StartDate -> Báo lỗi EndDate")]
    public void UTCID18_Validator_RejectsEqualDates()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.StartDate = new DateTime(2026, 10, 1);
        command.EndDate = command.StartDate;

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.EndDate));
    }

    [Fact(DisplayName = "UTCID19 (A) - Validator: EndDate trước StartDate -> Báo lỗi EndDate")]
    public void UTCID19_Validator_RejectsEndDateBeforeStartDate()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.StartDate = new DateTime(2026, 10, 2);
        command.EndDate = new DateTime(2026, 10, 1);

        var result = new UpdateVoucherValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.EndDate));
    }

    private static UpdateVoucherHandler Handler(IRepository<Voucher> repo, IUnitOfWork unitOfWork)
    {
        var mapper = new MapperConfiguration(config => config.AddProfile<VoucherMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new UpdateVoucherHandler(repo, unitOfWork, mapper);
    }

    private static (Mock<IRepository<Voucher>> Repo, Mock<IUnitOfWork> UnitOfWork) Dependencies(params Voucher[] rows)
    {
        var repo = new Mock<IRepository<Voucher>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Voucher, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Voucher, bool>> predicate, CancellationToken _) =>
                Task.FromResult(rows.FirstOrDefault(predicate.Compile())));
        repo.Setup(r => r.Update(It.IsAny<Voucher>()));
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (repo, unitOfWork);
    }

    private static UpdateVoucherCommand ValidCommand(Guid id) => new()
    {
        Id = id,
        Code = "NEWCODE",
        DiscountType = "PERCENTAGE",
        DiscountValue = 10,
        MinOrderValue = 0
    };

    private static Voucher ExistingVoucher(string code = "OLDCODE", bool active = true, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        DiscountType = "PERCENTAGE",
        DiscountValue = 5,
        MinOrderValue = null,
        StartDate = null,
        EndDate = null,
        IsActive = active,
        IsDeleted = deleted,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
