using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType;
using SmartPool.Domain.Enums;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Commands;

public class TicketTypeNameValidatorTests
{
    [Theory]
    [InlineData("Vé tháng 30 ngày", true)]
    [InlineData("Ve\u0301 lu\u031bo\u0323t 1", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("Vé tháng (30 ngày)", false)]
    [InlineData("/*/--*", false)]
    [InlineData("Vé_123", false)]
    [InlineData("Vé 🎟", false)]
    [InlineData("Vé\n", false)]
    [InlineData("Vé\ttháng", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Validate_Name_ShouldApplySameRulesToCreateAndUpdate(string? name, bool expectedValid)
    {
        var createResult = new CreateTicketTypeValidator().Validate(new CreateTicketTypeCommand
        {
            Name = name!,
            Price = 100000,
            TicketCategory = TicketCategoryEnum.VE_THANG
        });
        var updateResult = new UpdateTicketTypeValidator().Validate(new UpdateTicketTypeCommand
        {
            Id = Guid.NewGuid(),
            Name = name!,
            Price = 100000,
            TicketCategory = TicketCategoryEnum.VE_THANG
        });

        Assert.Equal(expectedValid, createResult.IsValid);
        Assert.Equal(expectedValid, updateResult.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(createResult.Errors, error => error.PropertyName == "Name");
            Assert.Contains(updateResult.Errors, error => error.PropertyName == "Name");
        }
    }
}
