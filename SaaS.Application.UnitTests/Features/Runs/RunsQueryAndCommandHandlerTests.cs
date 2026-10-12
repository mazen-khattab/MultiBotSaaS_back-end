using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Runs.Commands.Cancel;
using SaaS.Application.Features.Runs.Queries.GetAllRuns;
using SaaS.Application.Features.Runs.Queries.GetRunById;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Runs
{
    public class RunsQueryAndCommandHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"RunsDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task GetAllRuns_ShouldReturnScopedPaginatedRunsForUser()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            var run1 = new Run { Id = 1, UserId = user1Id, BotId = 1, Status = RunStatus.RUNNING.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-10), CollectedLeadsCount = 5 };
            var run2 = new Run { Id = 2, UserId = user1Id, BotId = 2, Status = RunStatus.COMPLETED.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-20), CollectedLeadsCount = 15 };
            var run3 = new Run { Id = 3, UserId = user2Id, BotId = 1, Status = RunStatus.RUNNING.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-5), CollectedLeadsCount = 2 };

            await dbContext.Runs.AddRangeAsync(run1, run2, run3);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetAllRunsQueryHandler(dbContext);

            // Act - Fetch all for user1
            var result = await handler.Handle(new GetAllRunsQuery(user1Id, null, 1, 10, null), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.TotalItems);
            Assert.Equal(2, result.Data.Items.Count());

            // Act - Filter by botId 1
            var filterResult = await handler.Handle(new GetAllRunsQuery(user1Id, 1, 1, 10, null), CancellationToken.None);

            // Assert
            Assert.True(filterResult.IsSuccess);
            Assert.Single(filterResult.Data!.Items);
            Assert.Equal(1, filterResult.Data.Items.First().Id);
        }

        [Fact]
        public async Task GetRunById_WhenBelongsToUser_ShouldReturnRun()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var run = new Run
            {
                Id = 10,
                UserId = userId,
                BotId = 1,
                GroupId = 5,
                AccountId = 2,
                Status = RunStatus.RUNNING.ToDbString(),
                CollectedLeadsCount = 12,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            await dbContext.Runs.AddAsync(run);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetRunByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetRunByIdQuery(userId, 10), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(10, result.Data.Id);
            Assert.Equal(12, result.Data.CollectedLeadsCount);
        }

        [Fact]
        public async Task GetRunById_WhenBelongsToAnotherUser_ShouldReturnNotFound()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            var run = new Run { Id = 10, UserId = user2Id, BotId = 1, Status = RunStatus.RUNNING.ToDbString() };
            await dbContext.Runs.AddAsync(run);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetRunByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetRunByIdQuery(user1Id, 10), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task CancelRun_WhenRunning_ShouldMarkCancelledAndRevertBusyAccount()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var account = new ConnectedAccount
            {
                Id = 100,
                UserId = userId,
                BotId = 1,
                Status = AccountStatus.BUSY.ToDbString(),
                IsActive = true
            };
            var run = new Run
            {
                Id = 50,
                UserId = userId,
                BotId = 1,
                AccountId = 100,
                Status = RunStatus.RUNNING.ToDbString(),
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Runs.AddAsync(run);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new CancelRunCommandHandler(dbContext);

            // Act
            var result = await handler.Handle(new CancelRunCommand(userId, 50), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            var updatedRun = await dbContext.Runs.FindAsync(50);
            Assert.Equal(RunStatus.CANCELLED.ToDbString(), updatedRun!.Status);
            Assert.NotNull(updatedRun.EndedAt);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(100);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount!.Status);
        }

        [Fact]
        public async Task CancelRun_WhenNotRunning_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var run = new Run
            {
                Id = 51,
                UserId = userId,
                BotId = 1,
                Status = RunStatus.COMPLETED.ToDbString(),
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                EndedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            await dbContext.Runs.AddAsync(run);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new CancelRunCommandHandler(dbContext);

            // Act
            var result = await handler.Handle(new CancelRunCommand(userId, 51), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("Cannot cancel run with status 'Completed'. Only running runs can be cancelled.", result.Message);
        }
    }
}
