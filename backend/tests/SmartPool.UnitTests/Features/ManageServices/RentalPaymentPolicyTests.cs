using SmartPool.Application.Features.ManageServices.Payments;

namespace SmartPool.UnitTests.Features.ManageServices;

public sealed class RentalPaymentPolicyTests
{
    [Theory]
    [InlineData("PENDING", true)]
    [InlineData("PARTIAL", true)]
    [InlineData("PAID", false)]
    [InlineData("ISSUED", false)]
    [InlineData("EXPIRED", false)]
    [InlineData("CANCELLED", false)]
    public void GivenRentalPaymentState_WhenCheckingUnpaidExpiry_ThenOnlyWaitingStatesExpire(string state, bool expectedWaiting)
    {
        var deadline = new DateTime(2026, 10, 28, 10, 5, 0, DateTimeKind.Utc);

        Assert.Equal(expectedWaiting, RentalPaymentPolicy.IsWaiting(state));
        Assert.Equal(expectedWaiting, RentalPaymentPolicy.HasExpired(state, deadline, deadline));
    }

    [Fact]
    public void GivenRentalPaymentBeforeDeadline_WhenCheckingExpiry_ThenItIsNotExpired()
    {
        var expiresAt = new DateTime(2026, 10, 28, 10, 5, 0, DateTimeKind.Utc);

        Assert.False(RentalPaymentPolicy.HasExpired(RentalPaymentPolicy.Partial, expiresAt, expiresAt.AddTicks(-1)));
    }

    [Theory]
    [InlineData(100000, 40000, 60000, 0)]
    [InlineData(100000, 100000, 0, 0)]
    [InlineData(100000, 110000, 0, 10000)]
    public void GivenGrossPaymentAndReceiptTotal_WhenCalculatingBalance_ThenFeeAndDepositRemainOneDueAmount(
        decimal due, decimal received, decimal expectedRemaining, decimal expectedExcess)
    {
        Assert.Equal(expectedRemaining, RentalPaymentPolicy.Remaining(due, received));
        Assert.Equal(expectedExcess, RentalPaymentPolicy.Excess(due, received));
    }

    [Fact]
    public void GivenOneRentalReference_WhenExtractingBankMemo_ThenTheCompleteReferenceIsReturned()
    {
        var reference = $"RN{Guid.NewGuid():N}".ToUpperInvariant();

        Assert.Equal(reference, RentalPaymentPolicy.ExtractReference(null, $"Chuyen tien {reference} SmartPool"));
        Assert.True(RentalPaymentPolicy.IsRentalReference(reference));
        Assert.False(RentalPaymentPolicy.IsTicketReference(reference));
    }

    [Fact]
    public void GivenOneTicketReference_WhenExtractingBankMemo_ThenItRemainsTicketOnly()
    {
        const string reference = "SPABC123";

        Assert.Equal(reference, RentalPaymentPolicy.ExtractReference(reference, "Thanh toan"));
        Assert.True(RentalPaymentPolicy.IsTicketReference(reference));
        Assert.False(RentalPaymentPolicy.IsRentalReference(reference));
    }

    [Fact]
    public void GivenAmbiguousOrMalformedReferences_WhenExtractingBankMemo_ThenNoDomainReferenceIsSelected()
    {
        var first = $"RN{Guid.NewGuid():N}";
        var second = $"RN{Guid.NewGuid():N}";

        Assert.Null(RentalPaymentPolicy.ExtractReference(null, $"{first} {second}"));
        Assert.Null(RentalPaymentPolicy.ExtractReference(null, "RN123 SPNOTHEX"));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(0, false)]
    [InlineData(-100, false)]
    [InlineData(1000.5, false)]
    public void GivenIncomingAmount_WhenCheckingWholeVnd_ThenOnlyPositiveIntegerAmountsAreAccepted(decimal amount, bool expected)
    {
        Assert.Equal(expected, RentalPaymentPolicy.IsWholeVnd(amount));
    }

    [Fact]
    public void GivenSameProviderEvent_WhenHashing_ThenOnlyAnIdenticalFinancialPayloadHasTheSameHash()
    {
        var original = new BankTransferEvent(42, "12345678", 80000, $"RN{Guid.NewGuid():N}", "BANK-REF-1");

        Assert.Equal(RentalPaymentPolicy.FinancialHash(original), RentalPaymentPolicy.FinancialHash(original));
        Assert.NotEqual(RentalPaymentPolicy.FinancialHash(original),
            RentalPaymentPolicy.FinancialHash(original with { Amount = original.Amount + 1 }));
        Assert.NotEqual(RentalPaymentPolicy.FinancialHash(original),
            RentalPaymentPolicy.FinancialHash(original with { BankReferenceCode = "BANK-REF-2" }));
    }
}
