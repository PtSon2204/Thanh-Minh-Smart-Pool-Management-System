using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SmartPool.IntegrationTests;

public sealed class PoolAccessMonthlyAdmissionTests
{
    [Fact]
    public async Task GivenFreshSingleUseTicketWithLegacyBalance_WhenConfirmingTwice_ThenItIsUsedOnceAndBalanceIsPreserved()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var ticket = await fixture.AddSingleUseTicketWithLegacyBalanceAsync(15);
            Assert.Equal(15, await fixture.ReadRemainingEntriesAsync(ticket.Id));
            var instant = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
            var lookup = await fixture.CreateOperations(instant).LookupAsync(ticket.QrCode, CancellationToken.None);
            using var lookupJson = JsonDocument.Parse(JsonSerializer.Serialize(lookup));

            Assert.True(lookupJson.RootElement.GetProperty("Found").GetBoolean());
            Assert.True(lookupJson.RootElement.GetProperty("IsValid").GetBoolean());
            Assert.Equal("Allowed", lookupJson.RootElement.GetProperty("Reason").GetString());

            var first = await fixture.CreateOperations(instant).ConfirmAsync(
                ticket.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            using var firstJson = JsonDocument.Parse(JsonSerializer.Serialize(first));
            var second = await fixture.CreateOperations(instant.AddSeconds(6)).ConfirmAsync(
                ticket.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            using var secondJson = JsonDocument.Parse(JsonSerializer.Serialize(second));
            var logs = await fixture.ReadTicketLogsAsync(ticket.Id);

            Assert.True(firstJson.RootElement.GetProperty("Allowed").GetBoolean());
            Assert.Equal("Allowed", firstJson.RootElement.GetProperty("Reason").GetString());
            Assert.Equal("Used", firstJson.RootElement.GetProperty("Ticket").GetProperty("Status").GetString());
            Assert.False(secondJson.RootElement.GetProperty("Allowed").GetBoolean());
            Assert.Equal("TicketInactive", secondJson.RootElement.GetProperty("Reason").GetString());
            Assert.Equal("Vé không còn hiệu lực.", secondJson.RootElement.GetProperty("Message").GetString());
            Assert.Equal("Used", await fixture.Context.Tickets.AsNoTracking()
                .Where(item => item.Id == ticket.Id).Select(item => item.Status).SingleAsync());
            Assert.Equal(15, await fixture.ReadRemainingEntriesAsync(ticket.Id));
            Assert.Equal(new[] { "Allowed", "Denied" }, logs.Select(log => log.Status));
            Assert.Equal(new[] { "Allowed", "TicketInactive" }, logs.Select(log => log.Message));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((2, 2, 2), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenFreshMonthlyTicket_WhenLookingUpBeforeEntry_ThenItRemainsEligibleWithoutWritingALog()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var operation = fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:00Z"));

            var response = await operation.LookupAsync(fixture.QrCode, CancellationToken.None);
            var json = JsonSerializer.Serialize(response);
            var observed = JsonDocument.Parse(json);

            Assert.True(observed.RootElement.GetProperty("Found").GetBoolean());
            Assert.True(observed.RootElement.GetProperty("IsValid").GetBoolean());
            Assert.Equal("Allowed", observed.RootElement.GetProperty("Reason").GetString());
            Assert.Equal(0, await fixture.Context.EntryLogs.CountAsync(log => log.TicketId == fixture.TicketId));
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenMalformedTicketCode_WhenLookingItUp_ThenItReturnsNotFoundWithoutWritingALog()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:00Z"))
                .LookupAsync("' OR 1=1 --", CancellationToken.None);
            using var observed = JsonDocument.Parse(JsonSerializer.Serialize(response));

            Assert.False(observed.RootElement.GetProperty("Found").GetBoolean());
            Assert.Equal("TicketNotFound", observed.RootElement.GetProperty("Reason").GetString());
            Assert.Empty(await fixture.ReadTicketLogsAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Theory]
    [InlineData("inactive_ticket", "TicketInactive")]
    [InlineData("inactive_type", "TicketTypeInactive")]
    [InlineData("future_issue", "IssueDateInFuture")]
    [InlineData("missing_monthly_expiry", "MonthlyExpiryMissing")]
    [InlineData("expired", "TicketExpired")]
    [InlineData("unsupported_category", "UnsupportedCategory")]
    public async Task GivenIneligibleTicket_WhenLookingItUp_ThenTheEligibilityReasonIsReturnedWithoutWriting(string scenario, string reason)
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            switch (scenario)
            {
                case "inactive_ticket":
                    await fixture.Context.Tickets.Where(ticket => ticket.Id == fixture.TicketId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(ticket => ticket.Status, "Used"));
                    break;
                case "inactive_type":
                    await fixture.Context.TicketTypes.Where(type => type.Id == fixture.TicketTypeId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(type => type.IsActive, false));
                    break;
                case "future_issue":
                    await fixture.Context.Tickets.Where(ticket => ticket.Id == fixture.TicketId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(ticket => ticket.IssueDate,
                            DateTime.SpecifyKind(new DateTime(2026, 10, 6), DateTimeKind.Utc)));
                    break;
                case "missing_monthly_expiry":
                    await fixture.Context.Tickets.Where(ticket => ticket.Id == fixture.TicketId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(ticket => ticket.ExpiryDate, (DateTime?)null));
                    break;
                case "expired":
                    await fixture.Context.Tickets.Where(ticket => ticket.Id == fixture.TicketId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(ticket => ticket.ExpiryDate,
                            DateTime.SpecifyKind(new DateTime(2026, 10, 4), DateTimeKind.Utc)));
                    break;
                case "unsupported_category":
                    await fixture.Context.TicketTypes.Where(type => type.Id == fixture.TicketTypeId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(type => type.TicketCategory, "UNSUPPORTED"));
                    break;
            }

            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:00Z"))
                .LookupAsync(fixture.QrCode, CancellationToken.None);

            Assert.False(response.IsValid);
            Assert.Equal(reason, response.Reason);
            Assert.Empty(await fixture.ReadTicketLogsAsync());
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenUnknownTicket_WhenConfirming_ThenItWritesOneDeniedLogWithoutATicketId()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        var instant = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        try
        {
            var result = await fixture.CreateOperations(instant).ConfirmAsync(
                $"UNKNOWN_{Guid.NewGuid():N}", "Qr", fixture.OperatorId, CancellationToken.None);
            var log = await fixture.Context.EntryLogs.SingleAsync(entry => entry.TicketId == null
                && entry.OperatorId == fixture.OperatorId && entry.ScanTime == instant.UtcDateTime);

            Assert.False(result.Allowed);
            Assert.Equal("TicketNotFound", result.Reason);
            Assert.Equal("Denied", log.Status);
            Assert.Equal("TicketNotFound", log.Message);
        }
        finally
        {
            await fixture.Context.EntryLogs.Where(entry => entry.TicketId == null
                && entry.OperatorId == fixture.OperatorId && entry.ScanTime == instant.UtcDateTime).ExecuteDeleteAsync();
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenAcceptedEntry_WhenConfirmingAgainAfterFiveSeconds_ThenItDeniesAndWritesDeniedLog()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var firstTime = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
            var first = await fixture.CreateOperations(firstTime).ConfirmAsync(
                fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            var second = await fixture.CreateOperations(firstTime.AddSeconds(6)).ConfirmAsync(
                fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            var json = JsonSerializer.Serialize(second);
            using var observed = JsonDocument.Parse(json);
            var logs = await fixture.ReadTicketLogsAsync();

            Assert.True(first.Allowed);
            Assert.False(observed.RootElement.GetProperty("Allowed").GetBoolean());
            Assert.Equal("AlreadyEnteredToday", observed.RootElement.GetProperty("Reason").GetString());
            Assert.Equal(new[] { "Allowed", "Denied" }, logs.Select(log => log.Status));
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Theory]
    [InlineData("allowed")]
    [InlineData("AlLoWeD")]
    public async Task GivenHistoricalAllowedLog_WhenLookingUpSameDay_ThenItIsInvalidWithoutWritingALog(string status)
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            await fixture.AddLogAsync(status, DateTime.SpecifyKind(new DateTime(2026, 10, 5, 9, 0, 0), DateTimeKind.Utc));

            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:00Z"))
                .LookupAsync(fixture.QrCode, CancellationToken.None);
            using var observed = JsonDocument.Parse(JsonSerializer.Serialize(response));
            var logs = await fixture.ReadTicketLogsAsync();

            Assert.False(observed.RootElement.GetProperty("IsValid").GetBoolean());
            Assert.Equal("AlreadyEnteredToday", observed.RootElement.GetProperty("Reason").GetString());
            Assert.Single(logs);
            Assert.Equal(status, logs[0].Status);
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenYesterdayAcceptedAndDeniedEntries_WhenConfirmingToday_ThenTodayIsAllowed()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            await fixture.AddLogAsync("Allowed", DateTime.SpecifyKind(new DateTime(2026, 10, 4, 17, 0, 0), DateTimeKind.Utc));
            var denied = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-04T18:00:00Z"))
                .ConfirmAsync(fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            var tomorrow = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T17:00:00Z"))
                .ConfirmAsync(fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);
            var logs = await fixture.ReadTicketLogsAsync();

            Assert.False(denied.Allowed);
            Assert.Equal("AlreadyEnteredToday", denied.Reason);
            Assert.True(tomorrow.Allowed);
            Assert.Equal(new[] { "Allowed", "Denied", "Allowed" }, logs.Select(log => log.Status));
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenAllowedLogBeforeVietnamMidnight_WhenCheckingAtMidnight_ThenNewVietnamDayIsEligible()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            await fixture.AddLogAsync("Allowed", DateTime.SpecifyKind(new DateTime(2026, 10, 4, 16, 59, 59), DateTimeKind.Utc));

            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-04T17:00:00Z"))
                .LookupAsync(fixture.QrCode, CancellationToken.None);

            Assert.True(response.IsValid);
            Assert.Equal("Allowed", response.Reason);
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenAllowedLogAtVietnamMidnight_WhenCheckingAtMidnight_ThenCurrentVietnamDayIsConsumed()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            await fixture.AddLogAsync("Allowed", DateTime.SpecifyKind(new DateTime(2026, 10, 4, 17, 0, 0), DateTimeKind.Utc));

            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-04T17:00:00Z"))
                .LookupAsync(fixture.QrCode, CancellationToken.None);

            Assert.False(response.IsValid);
            Assert.Equal("AlreadyEnteredToday", response.Reason);
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenTwoMonthlyTickets_WhenOneTicketHasAnAcceptedEntry_ThenTheOtherRemainsEligible()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var other = await fixture.AddTicketAsync();
            await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:00Z"))
                .ConfirmAsync(fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None);

            var response = await fixture.CreateOperations(DateTimeOffset.Parse("2026-10-05T10:00:06Z"))
                .LookupAsync(other.QrCode, CancellationToken.None);

            Assert.True(response.IsValid);
            Assert.Equal("Allowed", response.Reason);
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }

    [Fact]
    public async Task GivenTwoConcurrentConfirmations_WhenTheyTargetOneFreshMonthlyTicket_ThenOnlyOneIsAllowed()
    {
        await using var fixture = await AdmissionFixture.CreateAsync();
        try
        {
            var instant = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
            await using var firstContext = fixture.CreateContext();
            await using var secondContext = fixture.CreateContext();
            var firstOperation = fixture.CreateOperations(firstContext, instant);
            var secondOperation = fixture.CreateOperations(secondContext, instant);

            var results = await Task.WhenAll(
                firstOperation.ConfirmAsync(fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None),
                secondOperation.ConfirmAsync(fixture.QrCode, "Qr", fixture.OperatorId, CancellationToken.None));
            var json = JsonSerializer.Serialize(results);
            using var observed = JsonDocument.Parse(json);
            var logs = await fixture.ReadTicketLogsAsync();

            Assert.Equal(1, results.Count(result => result.Allowed));
            Assert.Equal(1, results.Count(result => !result.Allowed && result.Reason == "AlreadyEnteredToday"));
            Assert.Contains("AlreadyEnteredToday", json, StringComparison.Ordinal);
            Assert.Equal(1, logs.Count(log => log.Status == "Allowed"));
            Assert.Equal(1, logs.Count(log => log.Status == "Denied"));
            Assert.Equal(2, observed.RootElement.GetArrayLength());
        }
        finally
        {
            await fixture.CleanupAsync();
        }
    }
}
