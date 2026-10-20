using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Infrastructure.AttendanceQr;
using SmartPool.Infrastructure.Persistence.DbContext;
using StackExchange.Redis;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffAttendanceRedisResolutionTests
{
    [Fact]
    public async Task GivenExpiredOrStoppedToken_WhenRedisReturnsNil_ThenResolutionRejectsWithoutThrowing()
    {
        var database = new Mock<IDatabase>();
        database.Setup(value => value.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        await using var context = new SmartPoolDbContext(new DbContextOptionsBuilder<SmartPoolDbContext>().Options);
        var store = new RedisStaffAttendanceSessionStore(context, redis.Object, TimeProvider.System,
            Options.Create(new StaffAttendanceQrSettings()));

        var result = await store.ResolveAsync(StaffAttendanceQrToken.Create(Guid.NewGuid()), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }
}
