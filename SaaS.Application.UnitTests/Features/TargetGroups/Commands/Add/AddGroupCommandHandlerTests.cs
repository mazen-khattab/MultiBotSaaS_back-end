using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Commands.Add;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Commands.Add
{
    public class AddGroupCommandHandlerTests
    {
        private readonly Mock<IUserBotService> _userBotServiceMock;

        public AddGroupCommandHandlerTests()
        {
            _userBotServiceMock = new Mock<IUserBotService>();
        }

        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"AddGroupDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenUserDoesNotOwnBot_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var handler = new AddGroupCommandHandler(dbContext, _userBotServiceMock.Object);
            var dto = new AddGroupDto(botId, "Group A", "https://example.com/a", "{}", true);
            var command = new AddGroupCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("User or bot not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenUserOwnsBot_ShouldCreateGroupAndReturnSuccessWithUserId()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var groupId = 1;
            var botId = 2;

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new AddGroupCommandHandler(dbContext, _userBotServiceMock.Object);
            var dto = new AddGroupDto(botId, "Lawyers Group", "https://example.com/lawyers", "{\"city\":\"Cairo\"}", true);
            var command = new AddGroupCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(groupId, result.Data);
            Assert.Equal("Target group has been added successfully", result.Message);

            var group = await dbContext.TargetGroups.FirstOrDefaultAsync(g => g.UserId == userId);
            Assert.NotNull(group);
            Assert.Equal(userId, group.UserId);
            Assert.Equal(botId, group.BotId);
            Assert.Equal("Lawyers Group", group.GroupName);
            Assert.Equal("https://example.com/lawyers", group.GroupUrl);
            Assert.Equal("{\"city\":\"Cairo\"}", group.ConfigJson);
            Assert.True(group.IsActive);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_WhenConfigJsonIsNullOrWhiteSpace_ShouldDefaultToEmptyJsonObject(string? emptyConfigJson)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new AddGroupCommandHandler(dbContext, _userBotServiceMock.Object);
            var dto = new AddGroupDto(botId, "Group Empty Config", "https://example.com", emptyConfigJson, true);
            var command = new AddGroupCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);

            var group = await dbContext.TargetGroups.FirstOrDefaultAsync(g => g.UserId == userId);
            Assert.NotNull(group);
            Assert.Equal("{}", group.ConfigJson);
        }
    }
}
