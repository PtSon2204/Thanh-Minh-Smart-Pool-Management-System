using FluentValidation;
using Microsoft.AspNetCore.Http;
using Moq;
using SmartPool.Application.Features.ManageSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Commands.SubmitClassAttendance;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class SubmitClassAttendanceHandlerTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0x10, 0x20, 0xFF, 0xD9];
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [Fact(DisplayName = "UTCID01 (N) - Customer nộp JPG hợp lệ → trả PendingReview và gửi đúng bytes")]
    public async Task Handle_ValidJpeg_ReturnsPending()
    {
        var h = new Harness();
        var expected = h.Response();
        h.Operations.Setup(x => x.SubmitAttendanceAsync(h.ClassId, h.SessionId, h.CustomerId,
            It.Is<byte[]>(b => b.SequenceEqual(Jpeg)), "jpg", h.Token)).ReturnsAsync(expected);

        var actual = await h.Handle(h.Request(File(Jpeg, "image/jpeg")));

        Assert.Same(expected, actual);
        Assert.Equal("PendingReview", actual.Status);
        h.Operations.Verify(x => x.SubmitAttendanceAsync(h.ClassId, h.SessionId, h.CustomerId,
            It.Is<byte[]>(b => b.SequenceEqual(Jpeg)), "jpg", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Customer nộp PNG hợp lệ → format png")]
    public async Task Handle_ValidPng_UsesPngFormat()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.SubmitAttendanceAsync(h.ClassId, h.SessionId, h.CustomerId,
            It.Is<byte[]>(b => b.SequenceEqual(Png)), "png", h.Token)).ReturnsAsync(h.Response());

        await h.Handle(h.Request(File(Png, "image/png")));

        h.Operations.Verify(x => x.SubmitAttendanceAsync(h.ClassId, h.SessionId, h.CustomerId,
            It.Is<byte[]>(b => b.SequenceEqual(Png)), "png", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiếu Customer ID → từ chối trước khi đọc/lưu ảnh")]
    public async Task Handle_MissingUserId_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - User không có role CUSTOMER → từ chối")]
    public async Task Handle_NonCustomer_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Admin đồng thời có role CUSTOMER → từ chối")]
    public async Task Handle_AdminCustomer_Rejects()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("ADMIN")).Returns(true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Không có ảnh → lỗi validation, không truy vấn trạng thái")]
    public async Task Handle_MissingImage_Rejects()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() => h.Handle(h.Request(null)));

        Assert.Contains(error.Errors, x => x.PropertyName == "Image");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Chưa đăng ký lớp → truyền lỗi không tìm thấy")]
    public async Task Handle_NoRegistration_PropagatesNotFound()
    {
        var h = new Harness();
        h.Operations.Setup(x => x.GetAttendanceSubmissionContextAsync(h.ClassId, h.SessionId,
            h.CustomerId, h.Token)).ThrowsAsync(new KeyNotFoundException("Bạn chưa đăng ký lớp này."));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Handle());

        h.Operations.Verify(x => x.SubmitAttendanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), h.Token), Times.Never);
    }

    [Fact(DisplayName = "UTCID08 (A) - Đã nộp điểm danh → xung đột, không lưu lần hai")]
    public async Task Handle_AlreadySubmitted_Rejects()
    {
        var h = new Harness();
        h.SetContext(alreadySubmitted: true);

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());

        h.Operations.Verify(x => x.SubmitAttendanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), h.Token), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 (A) - Buổi chưa bắt đầu → xung đột, không lưu ảnh")]
    public async Task Handle_BeforeWindow_Rejects()
    {
        var h = new Harness();
        h.SetContext(startsAt: h.Now.AddMinutes(1));

        await Assert.ThrowsAsync<SwimClassStateConflictException>(() => h.Handle());

        h.Operations.Verify(x => x.SubmitAttendanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), h.Token), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 (A) - Bytes không có chữ ký ảnh → lỗi Image, không lưu")]
    public async Task Handle_InvalidImageBytes_Rejects()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            h.Handle(h.Request(File([1, 2, 3, 4, 5], "image/jpeg"))));

        Assert.Contains(error.Errors, x => x.PropertyName == "Image");
        h.Operations.Verify(x => x.SubmitAttendanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), h.Token), Times.Never);
    }

    [Fact(DisplayName = "UTCID11 (A) - Header PNG nhưng MIME JPG → lỗi Image, không lưu")]
    public async Task Handle_MimeMismatch_Rejects()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ValidationException>(() => h.Handle(h.Request(File(Png, "image/jpeg"))));

        h.Operations.Verify(x => x.SubmitAttendanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string>(), h.Token), Times.Never);
    }

    [Fact(DisplayName = "UTCID12 (A) - Lưu ảnh lỗi → truyền tiếp exception")]
    public async Task Handle_SubmitFailure_Propagates()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("Image storage unavailable");
        h.Operations.Setup(x => x.SubmitAttendanceAsync(h.ClassId, h.SessionId, h.CustomerId,
            It.IsAny<byte[]>(), "jpg", h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    private static IFormFile File(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "attendance.jpg")
        { Headers = new HeaderDictionary(), ContentType = contentType };

    private sealed class Harness
    {
        public readonly Guid CustomerId = Guid.NewGuid();
        public readonly Guid ClassId = Guid.NewGuid();
        public readonly Guid SessionId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly DateTime Now = new(2026, 11, 20, 0, 30, 0, DateTimeKind.Utc);
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly SubmitClassAttendanceHandler _handler;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CustomerId);
            CurrentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(true);
            SetContext();
            _handler = new SubmitClassAttendanceHandler(Operations.Object, CurrentUser.Object,
                new SubmitClassAttendanceValidator(), new FixedClock());
        }

        public void SetContext(bool alreadySubmitted = false, DateTime? startsAt = null)
        {
            Operations.Setup(x => x.GetAttendanceSubmissionContextAsync(ClassId, SessionId, CustomerId, Token))
                .ReturnsAsync(new AttendanceSubmissionContext(Guid.NewGuid(), "Confirmed", "Scheduled",
                    startsAt ?? Now.AddMinutes(-30), Now.AddMinutes(30), alreadySubmitted));
        }

        public SubmitClassAttendanceCommand Request(IFormFile? image) =>
            new() { ClassId = ClassId, SessionId = SessionId, Image = image };

        public ClassAttendanceResponse Response() =>
            new(Guid.NewGuid(), ClassId, SessionId, Guid.NewGuid(), "https://example.test/image.jpg",
                "PendingReview", Now, null, null, Now);

        public Task<ClassAttendanceResponse> Handle(SubmitClassAttendanceCommand? request = null) =>
            _handler.Handle(request ?? Request(File(Jpeg, "image/jpeg")), Token);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 20, 0, 30, 0, TimeSpan.Zero);
    }
}
