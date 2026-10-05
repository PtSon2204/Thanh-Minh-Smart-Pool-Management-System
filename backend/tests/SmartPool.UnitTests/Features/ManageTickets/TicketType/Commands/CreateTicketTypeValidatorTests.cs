using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Domain.Enums;
using Xunit;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Commands
{
    public class CreateTicketTypeValidatorTests
    {
        private readonly CreateTicketTypeValidator _validator;

        public CreateTicketTypeValidatorTests()
        {
            _validator = new CreateTicketTypeValidator();
        }

        [Fact]
        public void Validate_PriceIsNegative_ShouldReturnError()
        {
            var command = new CreateTicketTypeCommand 
            { 
                Name = "Vé Test", 
                Price = -50000, // Giá vé âm 
                TicketCategory = TicketCategoryEnum.VE_THANG 
            };

            var result = _validator.Validate(command);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Price");
        }

        [Fact]
        public void Validate_NameIsTooLong_ShouldReturnError()
        {
            var command = new CreateTicketTypeCommand 
            { 
                Name = new string('A', 256), // Tên dài 256 ký tự (vượt quá 255)
                Price = 100000, 
                TicketCategory = TicketCategoryEnum.VE_THANG 
            };

            var result = _validator.Validate(command);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == "Name");
        }

        [Theory]
        [InlineData(TicketCategoryEnum.VE_THANG)]
        [InlineData(TicketCategoryEnum.VE_LUOT)]
        public void Validate_SupportedTicketCategory_ShouldBeAccepted(TicketCategoryEnum category)
        {
            var result = _validator.Validate(new CreateTicketTypeCommand
            {
                Name = "Vé Test",
                Price = 100000,
                TicketCategory = category
            });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_UnsupportedTicketCategory_ShouldReturnError()
        {
            var result = _validator.Validate(new CreateTicketTypeCommand
            {
                Name = "Vé Test",
                Price = 100000,
                TicketCategory = (TicketCategoryEnum)2
            });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "TicketCategory");
        }
    }
}
