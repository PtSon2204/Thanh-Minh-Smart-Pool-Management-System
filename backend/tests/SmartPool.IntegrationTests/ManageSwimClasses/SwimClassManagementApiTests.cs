using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;
using SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.RegisterForSwimClass;
using SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Commands.CancelMyClassRegistration;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClassById;

namespace SmartPool.IntegrationTests.ManageSwimClasses;

public sealed class SwimClassManagementApiTests
{
    [Fact, Trait("TestCaseId", "SC-AUTH-01")]
    public async Task ClassManagementRequiresAuthenticationAndCoachPermission()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await f.Client.PostAsJsonAsync("/api/swim-classes", f.NewClass())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync("/api/swim-classes")).StatusCode);
        f.Use(f.OrdinaryCustomer);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await f.Client.PostAsJsonAsync("/api/swim-classes", f.NewClass())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/swim-classes")).StatusCode);
        await using var db = f.CreateDb();
        Assert.False(await db.SwimClasses.AnyAsync(c => c.Name.Contains(f.Suffix)));
    }

    [Fact, Trait("TestCaseId", "SC-CREATE-01")]
    public async Task EligibleCoachCreatesDraftClassWithNormalizedFields()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        f.Use(f.Owner);
        var response = await f.Client.PostAsJsonAsync("/api/swim-classes", new
        {
            name = $"  QA Class {f.Suffix}  ", description = "  Morning class  ",
            capacity = 3, fee = 150000m,
            registrationStartAt = DateTimeOffset.UtcNow.AddDays(-1),
            registrationEndAt = DateTimeOffset.UtcNow.AddDays(2)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateSwimClassResponse>())!;
        Assert.Equal($"QA Class {f.Suffix}", created.Name);
        Assert.Equal("Draft", created.Status);
        Assert.Equal(f.Owner.Id, created.CoachUserId);
        await using var db = f.CreateDb();
        var stored = await db.SwimClasses.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("Morning class", stored.Description);
        Assert.Equal(3, stored.Capacity);
        Assert.Equal(150000m, stored.Fee);
    }

    [Fact, Trait("TestCaseId", "SC-CREATE-02")]
    public async Task InvalidClassFieldsDoNotPersistDraft()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        f.Use(f.Owner);
        var response = await f.Client.PostAsJsonAsync("/api/swim-classes", new
        {
            name = " ", capacity = 6, fee = -1m,
            registrationStartAt = DateTimeOffset.UtcNow.AddDays(2),
            registrationEndAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = f.CreateDb();
        Assert.False(await db.SwimClasses.AnyAsync(c => c.CreatedByUserId == f.Owner.Id));
    }

    [Fact, Trait("TestCaseId", "SC-READ-01")]
    public async Task CoachSeesOwnDraftButOtherCoachDoesNot()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var detail = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        Assert.Equal(created.Id, detail!.Id);
        Assert.Equal("Draft", detail.Status);
        var own = await f.Client.GetFromJsonAsync<PagedResponse<SwimClassListItem>>(
            $"/api/swim-classes?searchTerm={f.Suffix}&scope=COACH&pageIndex=1&pageSize=1");
        Assert.Equal(created.Id, Assert.Single(own!.Items).Id);
        Assert.Equal(1, own.TotalCount);
        f.Use(f.OtherCoach);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync($"/api/swim-classes/{created.Id}")).StatusCode);
        var other = await f.Client.GetFromJsonAsync<PagedResponse<SwimClassListItem>>(
            $"/api/swim-classes?searchTerm={f.Suffix}");
        Assert.Empty(other!.Items);
    }

    [Fact, Trait("TestCaseId", "SC-READ-02")]
    public async Task InvalidListQueryAndUnknownClassReturnErrors()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        f.Use(f.Owner);
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync($"/api/swim-classes/{Guid.NewGuid()}")).StatusCode);
        foreach (var query in new[] { "pageIndex=0", "pageSize=101", "scope=ADMIN" })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await f.Client.GetAsync($"/api/swim-classes?{query}")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "SC-SESSION-01")]
    public async Task CreateSessionsRejectsOverlapWithoutPartialWrite()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var first = await f.AddSessionAsync(created.Id);
        Assert.Equal("Scheduled", first.Status);
        var start = SwimClassFixture.SessionAt(8);
        var response = await f.Client.PostAsJsonAsync($"/api/swim-classes/{created.Id}/sessions", new
        {
            sessions = new[]
            {
                new { startsAt = start, endsAt = start.AddHours(1) },
                new { startsAt = start.AddMinutes(30), endsAt = start.AddHours(2) }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal(1, await db.ClassSessions.CountAsync(s => s.ClassId == created.Id));
    }

    [Fact, Trait("TestCaseId", "SC-SESSION-02")]
    public async Task SessionMustBeFutureAndInsideVietnamShift()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        foreach (var start in new[] { SwimClassFixture.SessionAt(-1), SwimClassFixture.SessionAt(7, 12) })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsJsonAsync(
                $"/api/swim-classes/{created.Id}/sessions",
                new { sessions = new[] { new { startsAt = start, endsAt = start.AddHours(1) } } })).StatusCode);
        await using var db = f.CreateDb();
        Assert.False(await db.ClassSessions.AnyAsync(s => s.ClassId == created.Id));
    }

    [Fact, Trait("TestCaseId", "SC-ACTIVATE-01")]
    public async Task ActivationNeedsFutureSessionAndCannotBeRepeated()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        Assert.Equal(HttpStatusCode.Conflict,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        await f.AddSessionAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal("Active", await db.SwimClasses.Where(c => c.Id == created.Id).Select(c => c.Status).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-UPDATE-01")]
    public async Task OwnerUpdatesDraftAndStaleVersionOrOtherCoachIsRejected()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var detail = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        var payload = new
        {
            name = $"Updated {f.Suffix}", description = "Changed", capacity = 4, fee = 160000m,
            expectedUpdatedAt = Version(detail!.UpdatedAt),
            registrationStartAt = Version(created.RegistrationStartAt),
            registrationEndAt = Version(created.RegistrationEndAt)
        };
        var url = $"/api/swim-classes/{created.Id}";
        f.Use(f.OtherCoach);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PutAsJsonAsync(url, payload)).StatusCode);
        f.Use(f.Owner);
        var response = await f.Client.PutAsJsonAsync(url, payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UpdateSwimClassResponse>())!;
        Assert.Equal(payload.name, updated.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PutAsJsonAsync(url, payload)).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal(payload.name, await db.SwimClasses.Where(c => c.Id == created.Id).Select(c => c.Name).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-SESSION-03")]
    public async Task OwnerUpdatesAndCancelsSessionWithVersionChecks()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var session = await f.AddSessionAsync(created.Id);
        var url = $"/api/swim-classes/{created.Id}/sessions/{session.Id}";
        var start = SwimClassFixture.SessionAt(8);
        var before = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        var payload = new { startsAt = start, endsAt = start.AddHours(1),
            expectedUpdatedAt = Version(Assert.Single(before!.Sessions).UpdatedAt) };
        var update = await f.Client.PatchAsJsonAsync(url, payload);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var changed = (await update.Content.ReadFromJsonAsync<UpdateClassSessionResponse>())!;
        Assert.Equal(start.UtcDateTime, changed.StartsAt);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PatchAsJsonAsync(url, payload)).StatusCode);
        var after = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        var cancel = await f.Client.PatchAsJsonAsync(url + "/cancel",
            new { reason = "Pool maintenance", expectedUpdatedAt = Version(Assert.Single(after!.Sessions).UpdatedAt) });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<CancelClassSessionResponse>())!.Status);
        await using var db = f.CreateDb();
        Assert.Equal("Pool maintenance", await db.ClassSessions.Where(s => s.Id == session.Id)
            .Select(s => s.CancelReason).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-DEACTIVATE-01")]
    public async Task DeactivationCancelsFutureSessionsAndChecksVersion()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var session = await f.AddSessionAsync(created.Id);
        var detail = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        var url = $"/api/swim-classes/{created.Id}/deactivate";
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PatchAsJsonAsync(url,
            new { reason = "Closed", expectedUpdatedAt = Version(created.UpdatedAt) })).StatusCode);
        var response = await f.Client.PatchAsJsonAsync(url,
            new { reason = "Pool closed", expectedUpdatedAt = Version(detail!.UpdatedAt) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Inactive", (await response.Content.ReadFromJsonAsync<DeactivateSwimClassResponse>())!.Status);
        await using var db = f.CreateDb();
        Assert.Equal("Inactive", await db.SwimClasses.Where(c => c.Id == created.Id).Select(c => c.Status).SingleAsync());
        Assert.Equal("Cancelled", await db.ClassSessions.Where(s => s.Id == session.Id).Select(s => s.Status).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-REGISTER-01")]
    public async Task CustomerRequestsClassAndOwnerConfirmsRegistration()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        await f.AddSessionAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        f.Use(f.Student);
        var register = await f.Client.PostAsync($"/api/swim-classes/{created.Id}/registrations", null);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registration = (await register.Content.ReadFromJsonAsync<RegisterForSwimClassResponse>())!;
        Assert.Equal("PendingApproval", registration.RegistrationStatus);
        Assert.Equal(HttpStatusCode.Conflict,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/registrations", null)).StatusCode);
        f.Use(f.Owner);
        await using var db = f.CreateDb();
        var version = await db.ClassRegistrations.Where(r => r.Id == registration.RegistrationId)
            .Select(r => r.UpdatedAt).SingleAsync();
        var review = await f.Client.PatchAsJsonAsync(
            $"/api/swim-classes/{created.Id}/registrations/{registration.RegistrationId}/review",
            new { decision = "Accepted", expectedUpdatedAt = Version(version) });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal("Confirmed", (await review.Content.ReadFromJsonAsync<ReviewClassRegistrationResponse>())!.Status);
        Assert.Equal("Confirmed", await db.ClassRegistrations.AsNoTracking()
            .Where(r => r.Id == registration.RegistrationId).Select(r => r.Status).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-READ-03")]
    public async Task CustomerSeesActiveClassButNotDraft()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        f.Use(f.Student);
        Assert.Equal(HttpStatusCode.NotFound,
            (await f.Client.GetAsync($"/api/swim-classes/{created.Id}")).StatusCode);
        await f.AddSessionAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        f.Use(f.Student);
        var list = await f.Client.GetFromJsonAsync<PagedResponse<SwimClassListItem>>(
            $"/api/swim-classes?searchTerm={f.Suffix}&registrationOpenNow=true");
        Assert.Equal(created.Id, Assert.Single(list!.Items).Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.GetAsync($"/api/swim-classes/{created.Id}")).StatusCode);
    }

    [Fact, Trait("TestCaseId", "SC-REGISTER-02")]
    public async Task CustomerCanWithdrawPendingRegistrationAndReplayIsRejected()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        await f.AddSessionAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        f.Use(f.Student);
        Assert.Equal(HttpStatusCode.Created,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/registrations", null)).StatusCode);
        var url = $"/api/swim-classes/{created.Id}/registrations/me/cancel";
        var cancel = await f.Client.PatchAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<CancelMyClassRegistrationResponse>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PatchAsync(url, null)).StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal("Cancelled", await db.ClassRegistrations.Where(r => r.ClassId == created.Id)
            .Select(r => r.Status).SingleAsync());
    }

    [Fact, Trait("TestCaseId", "SC-SESSION-04")]
    public async Task ActiveClassCannotCancelItsLastFutureSession()
    {
        await using var f = await SwimClassFixture.CreateAsync();
        var created = await f.CreateClassAsync();
        var session = await f.AddSessionAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await f.Client.PostAsync($"/api/swim-classes/{created.Id}/activate", null)).StatusCode);
        var detail = await f.Client.GetFromJsonAsync<SwimClassDetailResponse>($"/api/swim-classes/{created.Id}");
        var response = await f.Client.PatchAsJsonAsync(
            $"/api/swim-classes/{created.Id}/sessions/{session.Id}/cancel",
            new { reason = "Schedule change", expectedUpdatedAt = Version(Assert.Single(detail!.Sessions).UpdatedAt) });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = f.CreateDb();
        Assert.Equal("Scheduled", await db.ClassSessions.Where(s => s.Id == session.Id)
            .Select(s => s.Status).SingleAsync());
    }

    private static DateTimeOffset Version(DateTime timestamp) => new(timestamp.ToUniversalTime());
}
