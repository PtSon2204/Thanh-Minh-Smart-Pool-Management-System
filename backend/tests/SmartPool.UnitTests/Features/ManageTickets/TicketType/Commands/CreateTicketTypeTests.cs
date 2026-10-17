using AutoMapper;
using Moq;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Commands
{
    public class CreateTicketTypeTests
    {
        private readonly Mock<IRepository<TicketTypeEntity>> _mockRepo;
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CreateTicketTypeHandler _handler;

        public CreateTicketTypeTests()
        {
            _mockRepo = new Mock<IRepository<TicketTypeEntity>>();
            _mockUow = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _handler = new CreateTicketTypeHandler(_mockRepo.Object, _mockUow.Object, _mockMapper.Object);
        }

        [Fact(DisplayName = "UTCID01 (N) - Tạo vé hợp lệ -> Lưu loại vé và trả về thông tin vé")]
        public async Task Handle_ValidRequest_ShouldAddEntityAndReturnResponse()
        {
            // Arrange
            var command = new CreateTicketTypeCommand
            {
                Name = "Vé tháng VIP",
                TicketCategory = TicketCategoryEnum.VE_THANG,
                Price = 500000,
                DurationDays = 30
            };

            // Giả lập Repository.AddAsync không làm gì cả hoặc lưu lại đối tượng được truyền vào
            TicketTypeEntity? addedEntity = null;
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<TicketTypeEntity>(), It.IsAny<CancellationToken>()))
                     .Callback<TicketTypeEntity, CancellationToken>((entity, ct) => addedEntity = entity)
                     .Returns(Task.CompletedTask);

            // Giả lập UoW.SaveChangesAsync thành công
            _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1); 

            // Giả lập Mapper trả về DTO
            var expectedResponse = new CreateTicketTypeResponse
            {
                Name = "Vé tháng VIP",
                TicketCategory = TicketCategoryEnum.VE_THANG,
                Price = 500000,
                DurationDays = 30
            };
            _mockMapper.Setup(m => m.Map<CreateTicketTypeResponse>(It.IsAny<TicketTypeEntity>()))
                       .Returns(expectedResponse);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<TicketTypeEntity>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

            Assert.NotNull(addedEntity);
            Assert.Equal("Vé tháng VIP", addedEntity.Name);
            Assert.Equal("VE_THANG", addedEntity.TicketCategory);
            Assert.Equal(500000, addedEntity.Price);
            Assert.Equal(30, addedEntity.DurationDays);

            //response
            Assert.NotNull(result);
            Assert.Equal("Vé tháng VIP", result.Name);
            Assert.Equal(TicketCategoryEnum.VE_THANG, result.TicketCategory);
            Assert.Equal(500000, result.Price);
            Assert.Equal(30, result.DurationDays);
        }

        [Fact(DisplayName = "UTCID02 (A) - Repository gặp lỗi -> Trả lỗi và không gọi lưu thay đổi")]
        public async Task Handle_WhenDatabaseThrowsException_ShouldThrowException()
        {
            // Arrange
            var command = new CreateTicketTypeCommand
            {
                Name = "Vé tháng VIP",
                TicketCategory = TicketCategoryEnum.VE_THANG,
                Price = 500000
            };

            // Giả lập Repository ném ra lỗi (ví dụ: đứt kết nối mạng / lỗi DB)
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<TicketTypeEntity>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new Exception("Database connection failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
            Assert.Equal("Database connection failed", exception.Message);
            _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact(DisplayName = "UTCID03 (N) - Tên tiếng Việt có dấu và số -> Hợp lệ")]
        public void Validate_VietnameseName()
        {
            var command = ValidCommand();
            command.Name = "Vé tháng 30 ngày";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID04 (N) - Tên tiếng Việt dùng dấu Unicode tổ hợp -> Hợp lệ")]
        public void Validate_DecomposedVietnameseName()
        {
            var command = ValidCommand();
            command.Name = "Ve\u0301 lu\u031bo\u0323t 1";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID05 (B) - Tên đúng 30 ký tự -> Hợp lệ")]
        public void Validate_NameAtLimit()
        {
            var command = ValidCommand();
            command.Name = new string('A', 30);

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID06 (B) - Tên 31 ký tự -> Báo lỗi Name")]
        public void Validate_NameOverLimit()
        {
            var command = ValidCommand();
            command.Name = new string('A', 31);

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID07 (A) - Tên chứa dấu ngoặc -> Báo lỗi Name")]
        public void Validate_NameWithParentheses()
        {
            var command = ValidCommand();
            command.Name = "Vé tháng (30 ngày)";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID08 (A) - Tên chứa ký tự đặc biệt -> Báo lỗi Name")]
        public void Validate_NameWithSpecialCharacters()
        {
            var command = ValidCommand();
            command.Name = "/*/--*";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID09 (A) - Tên chứa dấu gạch dưới -> Báo lỗi Name")]
        public void Validate_NameWithUnderscore()
        {
            var command = ValidCommand();
            command.Name = "Vé_123";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID10 (A) - Tên chứa emoji -> Báo lỗi Name")]
        public void Validate_NameWithEmoji()
        {
            var command = ValidCommand();
            command.Name = "Vé 🎟";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID11 (A) - Tên chứa ký tự xuống dòng -> Báo lỗi Name")]
        public void Validate_NameWithNewline()
        {
            var command = ValidCommand();
            command.Name = "Vé\n";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID12 (A) - Tên chứa tab -> Báo lỗi Name")]
        public void Validate_NameWithTab()
        {
            var command = ValidCommand();
            command.Name = "Vé\ttháng";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID13 (B) - Tên rỗng -> Báo lỗi Name")]
        public void Validate_EmptyName()
        {
            var command = ValidCommand();
            command.Name = "";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID14 (A) - Tên chỉ có khoảng trắng -> Báo lỗi Name")]
        public void Validate_WhitespaceName()
        {
            var command = ValidCommand();
            command.Name = "   ";

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID15 (A) - Tên null -> Báo lỗi Name")]
        public void Validate_NullName()
        {
            var command = ValidCommand();
            command.Name = null!;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }

        [Fact(DisplayName = "UTCID16 (N) - Phân loại VE_THANG -> Hợp lệ")]
        public void Validate_MonthlyCategory()
        {
            var command = ValidCommand();
            command.TicketCategory = TicketCategoryEnum.VE_THANG;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID17 (N) - Phân loại VE_LUOT -> Hợp lệ")]
        public void Validate_SingleUseCategory()
        {
            var command = ValidCommand();
            command.TicketCategory = TicketCategoryEnum.VE_LUOT;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID18 (A) - Phân loại không tồn tại -> Báo lỗi TicketCategory")]
        public void Validate_InvalidCategory()
        {
            var command = ValidCommand();
            command.TicketCategory = (TicketCategoryEnum)2;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "TicketCategory");
        }

        [Fact(DisplayName = "UTCID19 (A) - Giá vé âm -> Báo lỗi Price")]
        public void Validate_NegativePrice()
        {
            var command = ValidCommand();
            command.Price = -50000;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Price");
        }

        [Fact(DisplayName = "UTCID20 (B) - Giá vé bằng 0 -> Báo lỗi Price")]
        public void Validate_ZeroPrice()
        {
            var command = ValidCommand();
            command.Price = 0;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "Price");
        }

        [Fact(DisplayName = "UTCID21 (B) - Giá vé bằng 1 -> Hợp lệ")]
        public void Validate_MinimumPrice()
        {
            var command = ValidCommand();
            command.Price = 1;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID22 (N) - Hiệu lực null -> Hợp lệ")]
        public void Validate_UnlimitedDuration()
        {
            var command = ValidCommand();
            command.DurationDays = null;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID23 (B) - Hiệu lực 1 ngày -> Hợp lệ")]
        public void Validate_MinimumDuration()
        {
            var command = ValidCommand();
            command.DurationDays = 1;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.True(result.IsValid);
        }

        [Fact(DisplayName = "UTCID24 (B) - Hiệu lực bằng 0 -> Báo lỗi DurationDays")]
        public void Validate_ZeroDuration()
        {
            var command = ValidCommand();
            command.DurationDays = 0;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
        }

        [Fact(DisplayName = "UTCID25 (A) - Hiệu lực âm -> Báo lỗi DurationDays")]
        public void Validate_NegativeDuration()
        {
            var command = ValidCommand();
            command.DurationDays = -1;

            var result = new CreateTicketTypeValidator().Validate(command);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "DurationDays");
        }

        private static CreateTicketTypeCommand ValidCommand() => new()
        {
            Name = "Vé tháng VIP",
            TicketCategory = TicketCategoryEnum.VE_THANG,
            Price = 500000,
            DurationDays = 30
        };
    }
}

