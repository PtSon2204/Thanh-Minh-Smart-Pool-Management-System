using AutoMapper;
using Moq;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Mapping;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Commands
{
    public class CreateTicketTypeHandlerTests
    {
        private readonly Mock<IRepository<TicketTypeEntity>> _mockRepo;
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<IMapper> _mockMapper;
        private readonly CreateTicketTypeHandler _handler;

        public CreateTicketTypeHandlerTests()
        {
            _mockRepo = new Mock<IRepository<TicketTypeEntity>>();
            _mockUow = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _handler = new CreateTicketTypeHandler(_mockRepo.Object, _mockUow.Object, _mockMapper.Object);
        }

        [Fact]
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

        [Fact]
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
    }
}

