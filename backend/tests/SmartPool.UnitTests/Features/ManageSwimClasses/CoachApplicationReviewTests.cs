using Moq;
using SmartPool.Application.Features.ManageCoachApplications;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class CoachApplicationReviewTests
{
    [Theory]
    [InlineData(" approved ", "Approved", null)]
    [InlineData("REJECTED", "Rejected", "Ảnh mờ")]
    public async Task ValidDecision_IsNormalizedInApplication(
        string submittedDecision, string repositoryDecision, string? note)
    {
        var adminId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var operations = new Mock<ICoachApplicationOperations>();
        var expected = Item(applicationId, adminId, repositoryDecision);
        operations.Setup(x => x.ReviewAsync(applicationId, adminId,
            repositoryDecision, note, CancellationToken.None)).ReturnsAsync(expected);

        var result = await Handler(operations, adminId).Handle(
            new ReviewCoachApplicationCommand(applicationId, submittedDecision, note),
            CancellationToken.None);

        Assert.Same(expected, result);
        operations.VerifyAll();
    }

    [Fact]
    public async Task InvalidDecisionOrRejectionNote_DoesNotReachRepository()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        var handler = Handler(operations, Guid.NewGuid());
        foreach (var command in new[]
                 {
                     new ReviewCoachApplicationCommand(Guid.NewGuid(), "Pending", null),
                     new ReviewCoachApplicationCommand(Guid.NewGuid(), " ", null),
                     new ReviewCoachApplicationCommand(Guid.NewGuid(), "Rejected", "  "),
                     new ReviewCoachApplicationCommand(Guid.NewGuid(), "Approved", new string('a', 1001))
                 })
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
                handler.Handle(command, CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingAdminIdentity_DoesNotReview()
    {
        var operations = new Mock<ICoachApplicationOperations>(MockBehavior.Strict);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(operations, null)
            .Handle(new ReviewCoachApplicationCommand(Guid.NewGuid(), "Approved", null),
                CancellationToken.None));
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AThousandCharacterNote_IsAccepted()
    {
        var adminId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var note = new string('a', 1000);
        var operations = new Mock<ICoachApplicationOperations>();
        operations.Setup(x => x.ReviewAsync(applicationId, adminId, "Approved", note,
            CancellationToken.None)).ReturnsAsync(Item(applicationId, adminId, "Approved"));
        await Handler(operations, adminId).Handle(
            new ReviewCoachApplicationCommand(applicationId, "Approved", note),
            CancellationToken.None);
        operations.VerifyAll();
    }

    private static ReviewCoachApplicationHandler Handler(
        Mock<ICoachApplicationOperations> operations, Guid? adminId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(adminId);
        currentUser.Setup(x => x.IsInRole("ADMIN")).Returns(true);
        return new ReviewCoachApplicationHandler(operations.Object, currentUser.Object,
            new ReviewCoachApplicationValidator());
    }

    private static CoachApplicationItem Item(Guid id, Guid adminId, string status) =>
        new(id, Guid.NewGuid(), "Customer", status, DateTime.UtcNow,
            adminId, DateTime.UtcNow, null, null, null, null);
}
