using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Commands.Delete;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Commands.Delete
{
    public class DeleteGroupCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WhenGroupBelongsToAnotherUser_ShouldNotDeleteIt()
        {
            using var dbContext = CreateDbContext();
            dbContext.TargetGroups.Add(new TargetGroup { Id = 1, UserId = Guid.NewGuid() });
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var result = await new DeleteGroupCommandHandler(dbContext).Handle(
                new DeleteGroupCommand(Guid.NewGuid(), 1), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.NotNull(await dbContext.TargetGroups.FindAsync(1));
        }

        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"DeleteGroupDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenGroupNotFound_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var handler = new DeleteGroupCommandHandler(dbContext);
            var command = new DeleteGroupCommand(Guid.NewGuid(), 999);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Target group not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenGroupExists_ShouldRemoveGroupAndReturnSuccess()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var group = new TargetGroup
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                BotId = 1,
                GroupName = "Group to delete",
                GroupUrl = "https://example.com/delete",
                IsActive = true
            };

            await dbContext.TargetGroups.AddAsync(group);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new DeleteGroupCommandHandler(dbContext);
            var command = new DeleteGroupCommand(group.UserId, group.Id);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data);
            Assert.Equal("Target group deleted successfully", result.Message);

            var deletedGroup = await dbContext.TargetGroups.FindAsync(group.Id);
            Assert.Null(deletedGroup);
        }
    }
}
