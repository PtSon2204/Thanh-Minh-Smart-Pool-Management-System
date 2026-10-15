using System.Linq.Expressions;
using AutoMapper;
using Moq;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Queries;

public class GetTicketTypeByIdTests
{
    [Fact(DisplayName = "UTCID01 (N) - Id tồn tại -> Trả đúng thông tin loại vé và truyền CancellationToken")]
    public async Task Handle_ExistingId_ReturnsMatchingTicket()
    {
        var entity = TicketTypeTestSupport.Entity();
        var other = TicketTypeTestSupport.Entity("Vé khác");
        var repo = TicketTypeTestSupport.Repository(other, entity);
        using var cts = new CancellationTokenSource();
        var result = await new GetTicketTypeByIdHandler(repo.Object, TicketTypeTestSupport.Mapper())
            .Handle(new() { Id = entity.Id }, cts.Token);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.Name, result.Name);
        Assert.Equal(TicketCategoryEnum.VE_THANG, result.TicketCategory);
        Assert.Equal(entity.Price, result.Price);
        Assert.Equal(entity.DurationDays, result.DurationDays);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
        Assert.Equal(entity.UpdatedAt, result.UpdatedAt);
        Assert.True(result.IsActive);
        Assert.Null(result.LockedAt);
        repo.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketTypeEntity, bool>>>(), cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Id không tồn tại -> Báo không tìm thấy và không gọi mapper")]
    public Task Handle_MissingId_Throws() => AssertNotFound(Guid.NewGuid(), TicketTypeTestSupport.Entity());

    [Fact(DisplayName = "UTCID03 (A) - Vé đã xóa mềm -> Báo không tìm thấy")]
    public Task Handle_DeletedTicket_Throws()
    {
        var entity = TicketTypeTestSupport.Entity(deleted: true);
        return AssertNotFound(entity.Id, entity);
    }

    [Fact(DisplayName = "UTCID04 (B) - Id rỗng -> Báo không tìm thấy")]
    public Task Handle_EmptyId_Throws() => AssertNotFound(Guid.Empty, TicketTypeTestSupport.Entity());

    private static async Task AssertNotFound(Guid id, TicketTypeEntity entity)
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var repo = TicketTypeTestSupport.Repository(entity);
        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new GetTicketTypeByIdHandler(repo.Object, mapper.Object).Handle(new() { Id = id }, CancellationToken.None));
        Assert.Equal($"Không tìm thấy loại vé với Id: {id}", error.Message);
        mapper.Verify(m => m.Map<GetTicketTypeByIdResponse>(It.IsAny<object>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (N) - Trạng thái và cờ xóa null -> Trả vé hoạt động")]
    public async Task Handle_NullFlags_ReturnsActiveTicket()
    {
        var entity = TicketTypeTestSupport.Entity(active: null, deleted: null);
        var result = await new GetTicketTypeByIdHandler(TicketTypeTestSupport.Repository(entity).Object, TicketTypeTestSupport.Mapper())
            .Handle(new() { Id = entity.Id }, CancellationToken.None);
        Assert.True(result.IsActive);
        Assert.Null(result.LockedAt);
    }

    [Fact(DisplayName = "UTCID06 (N) - Vé bị khóa -> Trả chi tiết và LockedAt bằng UpdatedAt")]
    public async Task Handle_LockedTicket_ReturnsLockTime()
    {
        var entity = TicketTypeTestSupport.Entity(category: "VE_LUOT", active: false);
        entity.UpdatedAt = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        entity.DurationDays = null;
        var result = await new GetTicketTypeByIdHandler(TicketTypeTestSupport.Repository(entity).Object, TicketTypeTestSupport.Mapper())
            .Handle(new() { Id = entity.Id }, CancellationToken.None);
        Assert.False(result.IsActive);
        Assert.Equal(entity.UpdatedAt, result.LockedAt);
        Assert.Equal(TicketCategoryEnum.VE_LUOT, result.TicketCategory);
        Assert.Null(result.DurationDays);
    }

    [Fact(DisplayName = "UTCID07 (A) - Repository gặp lỗi -> Truyền lỗi và không gọi mapper")]
    public async Task Handle_RepositoryFailure_PropagatesException()
    {
        var repo = TicketTypeTestSupport.Repository();
        var failure = new InvalidOperationException("Database unavailable");
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TicketTypeEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetTicketTypeByIdHandler(repo.Object, mapper.Object).Handle(new() { Id = Guid.NewGuid() }, CancellationToken.None)));
        mapper.Verify(m => m.Map<GetTicketTypeByIdResponse>(It.IsAny<object>()), Times.Never);
    }
}
