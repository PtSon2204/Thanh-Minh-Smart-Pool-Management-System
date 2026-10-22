using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffAttendanceQrContractTests
{
    [Fact]
    public void GivenOpaqueQrPayload_WhenParsingWrongPrefixOrTruncatedValue_ThenItIsRejected()
    {
        Assert.False(StaffAttendanceQrToken.TryParse("SMARTPOOL:STAFF:V2:abc", out _));
        Assert.False(StaffAttendanceQrToken.TryParse("SMARTPOOL:STAFF:V1:", out _));
        Assert.False(StaffAttendanceQrToken.TryParseAction("0", out _));
    }

    [Fact]
    public void GivenIssuedQrSession_WhenItsGenerationExpiresOrSessionStops_ThenItCannotResolve()
    {
        var now = new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero);
        var state = new StaffAttendanceQrSessionState(now, now.AddMinutes(15));
        var settings = new StaffAttendanceQrSettings();

        Assert.True(state.CanResolve(now.AddSeconds(59), settings));
        Assert.False(state.CanResolve(now.AddSeconds(60), settings));
        Assert.False(state.Stop().CanResolve(now.AddSeconds(1), settings));
    }

    [Fact]
    public void GivenActiveQrSession_WhenGenerationRotates_ThenWaitForTheFullTokenLifetime()
    {
        var now = new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero);
        var state = new StaffAttendanceQrSessionState(now, now.AddMinutes(15));

        var settings = new StaffAttendanceQrSettings();
        Assert.False(state.CanRotate(1, now.AddSeconds(59), settings));
        Assert.True(state.CanRotate(1, now.AddSeconds(60), settings));
        Assert.True(state.CanResolve(now.AddSeconds(59), settings));
        Assert.False(state.CanResolve(now.AddSeconds(60), settings));
    }

    [Fact]
    public void GivenStaleGenerationOrActionWindowEnd_WhenRotatingOrIssuing_ThenTheContractRejectsTheStaleGenerationAndCapsExpiry()
    {
        var now = new DateTimeOffset(2026, 10, 18, 7, 0, 0, TimeSpan.Zero);
        var settings = new StaffAttendanceQrSettings();
        var state = new StaffAttendanceQrSessionState(now, now.AddMinutes(15), Generation: 4);

        Assert.False(state.CanRotate(expectedGeneration: 3, now, settings));
        Assert.True(state.CanRotate(expectedGeneration: 4, now.AddSeconds(60), settings));
        Assert.Equal(now.AddSeconds(30), StaffAttendanceQrTiming.CapTokenExpiry(now, now.AddMinutes(15), now.AddSeconds(30), settings));
    }

    [Fact]
    public async Task GivenResolvedSession_WhenPreviewing_ThenTheResponseUsesVerifiedPurposeAndServerSessionContext()
    {
        var sender = new CapturingSender();
        var sessionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var expires = new DateTime(2026, 10, 18, 7, 1, 0, DateTimeKind.Utc);
        var controller = CreateController(sender, RoleEnum.SALES, store: new ResolvingSessionStore(
            Guid.NewGuid(), true, sessionId, shiftId, new DateOnly(2026, 10, 18), expires, expires.AddMinutes(10)));

        var result = await controller.Preview(new StaffAttendanceQrPayloadRequest { QrPayload = "ignored" }, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result).Value;
        Assert.IsType<StaffAttendanceQrPreviewResponse>(response);
        var preview = Assert.IsType<StaffAttendanceQrPreviewResponse>(response);
        Assert.Equal("StaffAttendance", preview.Purpose);
        Assert.Equal(sessionId, preview.SessionId);
        Assert.Equal(shiftId, preview.ShiftId);
        Assert.Equal(new DateOnly(2026, 10, 18), preview.WorkDate);
        Assert.Equal(StaffAttendanceQrAction.CheckIn, preview.Action);
        Assert.Equal(expires, preview.TokenExpiresAtUtc);
        Assert.Equal(expires.AddMinutes(10), preview.SessionExpiresAtUtc);
    }

    [Fact]
    public void GivenScheduledAssignmentWithInactiveShiftTemplate_WhenCheckingQrEligibility_ThenTheSavedAssignmentRemainsEligible()
    {
        var assignment = new EmployeeShift { Status = "Scheduled", Shift = new Shift { IsActive = false } };

        Assert.True(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckIn));
        assignment.Status = "Cancelled";
        Assert.False(StaffAttendanceQrAssignmentPolicy.SupportsAction(assignment, StaffAttendanceQrAction.CheckIn));
    }

    [Fact]
    public async Task GivenNonStaffRoleOrInvalidQr_WhenPreviewingOrScanning_ThenTheControllerDoesNotSendAttendance()
    {
        var sender = new CapturingSender();
        var controller = CreateController(sender, RoleEnum.CUSTOMER);

        var preview = await controller.Preview(new StaffAttendanceQrPayloadRequest { QrPayload = "invalid" }, CancellationToken.None);
        var scan = await controller.Scan(new StaffAttendanceQrPayloadRequest { QrPayload = "invalid" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(preview);
        Assert.IsType<ForbidResult>(scan);
        Assert.Null(sender.AttendanceCommand);
    }

    [Theory]
    [InlineData(RoleEnum.SALES)]
    [InlineData(RoleEnum.COACH)]
    public async Task GivenResolvedOwnAssignment_WhenEligibleEmployeeScans_ThenTheControllerSendsOnlyServerOwnedQrAttendanceValues(RoleEnum role)
    {
        var employeeId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var sender = new CapturingSender();
        var controller = CreateController(sender, role, employeeId, new ResolvingSessionStore(assignmentId, true));

        var result = await controller.Scan(new StaffAttendanceQrPayloadRequest { QrPayload = "accepted-by-fake-store" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(sender.AttendanceCommand);
        Assert.Equal(assignmentId, sender.AttendanceCommand.Id);
        Assert.Equal(employeeId, sender.AttendanceCommand.OwnerId);
        Assert.Equal(employeeId, sender.AttendanceCommand.PerformedByUserId);
        Assert.True(sender.AttendanceCommand.CheckIn);
        Assert.Equal("QR", sender.AttendanceCommand.Method);
    }

    [Fact]
    public async Task GivenRevokedIssuerOrNoOwnAssignment_WhenSalesScans_ThenTheControllerDoesNotSendAttendance()
    {
        var sender = new CapturingSender();
        var controller = CreateController(sender, RoleEnum.SALES);

        var result = await controller.Scan(new StaffAttendanceQrPayloadRequest { QrPayload = "rejected-by-fake-store" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(sender.AttendanceCommand);
    }

    [Fact]
    public void GivenQrAttendanceEndpoints_WhenInspectingContracts_ThenEachRequiresAttendanceUpdateAndExposesNoClientIdentityOrTimestamp()
    {
        var methods = typeof(StaffAttendanceQrController).GetMethods()
            .Where(method => method.DeclaringType == typeof(StaffAttendanceQrController)).ToArray();

        Assert.All(methods.Where(method => method.Name is "CreateSession" or "RotateSession" or "StopSession" or "Preview" or "Scan"), method =>
            Assert.Equal("permission:attendance:update", method.GetCustomAttributes(typeof(RequirePermissionAttribute), false)
                .Cast<RequirePermissionAttribute>().Single().Policy));
        Assert.DoesNotContain(typeof(StaffAttendanceQrPayloadRequest).GetProperties(), property => property.Name is "EmployeeId" or "Timestamp");
    }

    private static StaffAttendanceQrController CreateController(ISender sender, RoleEnum role, Guid? userId = null, IStaffAttendanceSessionStore? store = null)
    {
        var controller = new StaffAttendanceQrController(sender, store ?? new RejectingSessionStore())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()), new Claim(ClaimTypes.Role, role.ToString())], "test"))
                }
            }
        };
        return controller;
    }

    private sealed class RejectingSessionStore : IStaffAttendanceSessionStore
    {
        public Task<StaffAttendanceQrIssuedSession> CreateAsync(StaffAttendanceQrIssueRequest request, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StaffAttendanceQrIssuedSession> RotateAsync(Guid sessionId, long expectedGeneration, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task StopAsync(Guid sessionId, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StaffAttendanceQrResolution?> ResolveAsync(string qrPayload, Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<StaffAttendanceQrResolution?>(null);
    }

    private sealed class ResolvingSessionStore(Guid assignmentId, bool checkIn, Guid? sessionId = null, Guid? shiftId = null,
        DateOnly? workDate = null, DateTime? tokenExpiresAtUtc = null, DateTime? sessionExpiresAtUtc = null) : IStaffAttendanceSessionStore
    {
        public Task<StaffAttendanceQrIssuedSession> CreateAsync(StaffAttendanceQrIssueRequest request, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StaffAttendanceQrIssuedSession> RotateAsync(Guid sessionId, long expectedGeneration, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task StopAsync(Guid sessionId, Guid issuerId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StaffAttendanceQrResolution?> ResolveAsync(string qrPayload, Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<StaffAttendanceQrResolution?>(new(
            assignmentId, sessionId ?? Guid.NewGuid(), shiftId ?? Guid.NewGuid(), workDate ?? new DateOnly(2026, 10, 18),
            checkIn ? StaffAttendanceQrAction.CheckIn : StaffAttendanceQrAction.CheckOut,
            tokenExpiresAtUtc ?? DateTime.UnixEpoch.AddMinutes(1), sessionExpiresAtUtc ?? DateTime.UnixEpoch.AddMinutes(15)));
    }

    private sealed class CapturingSender : ISender
    {
        public RecordAttendanceCommand? AttendanceCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RecordAttendanceCommand command)
            {
                AttendanceCommand = command;
                return Task.FromResult((TResponse)(object)new RecordAttendanceResponse());
            }
            throw new InvalidOperationException("Unexpected request type.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
