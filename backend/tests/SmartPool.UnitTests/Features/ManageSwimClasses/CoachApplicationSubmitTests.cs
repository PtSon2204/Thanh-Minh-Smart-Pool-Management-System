using Microsoft.AspNetCore.Http;
using Moq;
using SmartPool.Application.Features.ManageCoachApplications;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationSubmitTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0x10, 0x20, 0xFF, 0xD9];
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [Theory]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("image/png", "png")]
    public async Task ValidImage_ReachesRepositoryWithCurrentCustomer(string mime, string format)
    {
        var userId = Guid.NewGuid();
        var bytes = format == "jpg" ? Jpeg : Png;
        var operations = new Mock<ICoachApplicationOperations>();
        var expected = Item(userId);
        operations.Setup(x => x.SubmitAsync(userId,
            It.Is<byte[]>(value => value.SequenceEqual(bytes)), format, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await Handler(operations, userId).Handle(
            new SubmitCoachApplicationCommand(File(bytes, mime)), CancellationToken.None);

        Assert.Same(expected, result);
        operations.VerifyAll();
    }

    [Fact]
    public async Task MissingIdentity_DoesNotStoreCertificate()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(operations, null)
            .Handle(new SubmitCoachApplicationCommand(File(Jpeg, "image/jpeg")), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidFileMetadata_DoesNotStoreCertificate()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var handler = Handler(operations, Guid.NewGuid());
        foreach (var file in new IFormFile?[]
                 {
                     null, File([0xFF, 0xD8, 0xD9], "image/jpeg"),
                     File(Jpeg, "image/gif"),
                     File(new byte[SubmitCoachApplicationValidator.MaxImageBytes + 1], "image/png")
                 })
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => handler.Handle(
                new SubmitCoachApplicationCommand(file), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidSignatureOrMimeMismatch_DoesNotStoreCertificate()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var handler = Handler(operations, Guid.NewGuid());
        foreach (var file in new[]
                 { File([1, 2, 3, 4, 5], "image/jpeg"), File(Png, "image/jpeg") })
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => handler.Handle(
                new SubmitCoachApplicationCommand(file), CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    private static SubmitCoachApplicationHandler Handler(
        Mock<ICoachApplicationOperations> operations, Guid? userId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsInRole("CUSTOMER")).Returns(true);
        return new SubmitCoachApplicationHandler(operations.Object, currentUser.Object,
            new SubmitCoachApplicationValidator());
    }

    private static IFormFile File(byte[] bytes, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "certificate", "certificate.jpg")
        { Headers = new HeaderDictionary(), ContentType = contentType };

    private static CoachApplicationItem Item(Guid userId) => new(Guid.NewGuid(), userId,
        "Customer", "Pending", DateTime.UtcNow, null, null, null, null, null, null);
}
