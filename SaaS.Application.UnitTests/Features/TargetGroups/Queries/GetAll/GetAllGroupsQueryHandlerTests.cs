using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Queries.GetAll;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Queries.GetAll
{
    public class GetAllGroupsQueryHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"GetAllGroupsDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenNoGroupsFoundForUser_ShouldReturnEmptyList()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var userBotService = new Mock<IUserBotService>();
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, 1, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllGroupsQueryHandler(dbContext, userBotService.Object);
            var query = new GetAllGroupsQuery(userId, 1);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data);
            Assert.Equal("No target groups found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenGroupsExistForUser_ShouldReturnMappedGroupList()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var botId = 1;

            var group1 = new TargetGroup
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                GroupName = "Group 1",
                GroupUrl = "https://example.com/1",
                IsActive = true
            };

            var group2 = new TargetGroup
            {
                Id = 2,
                UserId = userId,
                BotId = botId,
                GroupName = "Group 2",
                GroupUrl = "https://example.com/2",
                IsActive = false
            };

            var differentBotGroup = new TargetGroup
            {
                Id = 3,
                UserId = userId,
                BotId = 2,
                GroupName = "Different Bot Group",
                GroupUrl = "https://example.com/diff-bot",
                IsActive = true
            };

            var otherUserGroup = new TargetGroup
            {
                Id = 4,
                UserId = otherUserId,
                BotId = botId,
                GroupName = "Other Group",
                GroupUrl = "https://example.com/other",
                IsActive = true
            };

            await dbContext.TargetGroups.AddRangeAsync(group1, group2, differentBotGroup, otherUserGroup);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var userBotService = new Mock<IUserBotService>();
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, botId, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllGroupsQueryHandler(dbContext, userBotService.Object);
            var query = new GetAllGroupsQuery(userId, botId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count);
            Assert.Equal("Target groups have been retrieved successfully", result.Message);

            Assert.Contains(result.Data, g => g.Id == 1 && g.GroupName == "Group 1" && g.IsActive);
            Assert.Contains(result.Data, g => g.Id == 2 && g.GroupName == "Group 2" && !g.IsActive);
            Assert.DoesNotContain(result.Data, g => g.Id == 3);
            Assert.DoesNotContain(result.Data, g => g.Id == 4);
        }

        [Fact]
        public async Task Handle_WhenUserDoesNotOwnBot_ShouldReturnNotFoundFailure()
        {
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            using var cancellation = new CancellationTokenSource();
            var userBotService = new Mock<IUserBotService>(MockBehavior.Strict);
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, 1, cancellation.Token))
                .ReturnsAsync(false);
            var handler = new GetAllGroupsQueryHandler(dbContext, userBotService.Object);

            var result = await handler.Handle(new GetAllGroupsQuery(userId, 1), cancellation.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("User or bot not found", result.Message);
            Assert.Null(result.Data);
            userBotService.VerifyAll();
        }
    }
}
