using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Queries.GetById;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Queries.GetById
{
    public class GetGroupByIdQueryHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"GetGroupByIdDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenGroupNotFound_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var handler = new GetGroupByIdQueryHandler(dbContext);
            var query = new GetGroupByIdQuery(Guid.NewGuid(), 1, 999);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Target group not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenGroupBelongsToDifferentUser_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var groupId = 10;
            var botId = 1;

            var group = new TargetGroup
            {
                Id = groupId,
                UserId = ownerId,
                BotId = botId,
                GroupName = "Marketing Agency",
                GroupUrl = "https://example.com/agency"
            };

            await dbContext.TargetGroups.AddAsync(group);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetGroupByIdQueryHandler(dbContext);
            var query = new GetGroupByIdQuery(otherUserId, botId, groupId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Target group not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenGroupBelongsToDifferentBot_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var groupId = 10;

            var group = new TargetGroup
            {
                Id = groupId,
                UserId = userId,
                BotId = 1,
                GroupName = "Marketing Agency",
                GroupUrl = "https://example.com/agency"
            };

            await dbContext.TargetGroups.AddAsync(group);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetGroupByIdQueryHandler(dbContext);
            var query = new GetGroupByIdQuery(userId, 2, groupId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Target group not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenGroupExists_ShouldReturnGroupDetailsWithLeadsAndRunsCount()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var groupId = 10;

            var group = new TargetGroup
            {
                Id = groupId,
                UserId = userId,
                BotId = 1,
                GroupName = "Marketing Agency",
                GroupUrl = "https://example.com/agency",
                ConfigJson = "{\"filter\": \"active\"}",
                IsActive = true,
                LastCursor = "cursor-abc"
            };

            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = 1,
                GroupId = groupId,
                ProfileName = "Lead 1"
            };

            var lead2 = new Lead
            {
                Id = 2,
                UserId = userId,
                BotId = 1,
                GroupId = groupId,
                ProfileName = "Lead 2"
            };

            var run1 = new Run
            {
                Id = 1,
                UserId = userId,
                BotId = 1,
                GroupId = groupId,
                Status = "Completed"
            };

            await dbContext.TargetGroups.AddAsync(group);
            await dbContext.Leads.AddRangeAsync(lead1, lead2);
            await dbContext.Runs.AddAsync(run1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetGroupByIdQueryHandler(dbContext);
            var query = new GetGroupByIdQuery(userId, 1, groupId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Target group retrieved successfully", result.Message);

            Assert.Equal(groupId, result.Data.Id);
            Assert.Equal("Marketing Agency", result.Data.GroupName);
            Assert.Equal("https://example.com/agency", result.Data.GroupUrl);
            Assert.Equal("{\"filter\": \"active\"}", result.Data.ConfigJson);
            Assert.True(result.Data.IsActive);
            Assert.Equal(2, result.Data.RelatedLeadsCount);
            Assert.Equal(1, result.Data.RunsCount);
        }
    }
}
