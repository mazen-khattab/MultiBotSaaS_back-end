using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Scrapes.Commands.Cancel;
using SaaS.Application.Features.Scrapes.Queries.GetAllScrapes;
using SaaS.Application.Features.Scrapes.Queries.GetScrapeById;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes
{
    public class ScrapesQueryAndCommandHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"ScrapesDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task GetAllScrapes_ShouldReturnScopedPaginatedScrapesForUser()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            var scrape1 = new Scrape { Id = 1, UserId = user1Id, BotId = 1, Status = ScrapeStatus.RUNNING.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-10), CollectedLeadsCount = 5 };
            var scrape2 = new Scrape { Id = 2, UserId = user1Id, BotId = 2, Status = ScrapeStatus.COMPLETED.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-20), CollectedLeadsCount = 15 };
            var scrape3 = new Scrape { Id = 3, UserId = user2Id, BotId = 1, Status = ScrapeStatus.RUNNING.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-5), CollectedLeadsCount = 2 };
            var scrape4 = new Scrape { Id = 4, UserId = user1Id, BotId = 1, Status = ScrapeStatus.COMPLETED.ToDbString(), StartedAt = DateTime.UtcNow.AddMinutes(-30), CollectedLeadsCount = 10 };

            await dbContext.Scrapes.AddRangeAsync(scrape1, scrape2, scrape3, scrape4);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var userBotService = new Mock<IUserBotService>(MockBehavior.Strict);
            userBotService.Setup(service => service.CheckOwnershipAsync(user1Id, 1, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllScrapesQueryHandler(dbContext, userBotService.Object);

            // Fetch the first page for this user and bot, ordered by start time.
            var result = await handler.Handle(new GetAllScrapesQuery(user1Id, 1, 1, 1), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.TotalItems);
            Assert.Equal(2, result.Data.TotalPages);
            Assert.Equal(1, result.Data.CurrentPage);
            Assert.Equal(1, Assert.Single(result.Data.Items).Id);

            var secondPage = await handler.Handle(new GetAllScrapesQuery(user1Id, 1, 2, 1), CancellationToken.None);
            Assert.True(secondPage.IsSuccess);
            Assert.NotNull(secondPage.Data);
            Assert.Equal(2, secondPage.Data.TotalItems);
            Assert.Equal(2, secondPage.Data.TotalPages);
            Assert.Equal(2, secondPage.Data.CurrentPage);
            Assert.Equal(4, Assert.Single(secondPage.Data.Items).Id);

            // Status filtering must also preserve user and bot scope.
            var filterResult = await handler.Handle(new GetAllScrapesQuery(user1Id, 1, 1, 10, ScrapeStatus.COMPLETED.ToDbString()), CancellationToken.None);

            // Assert
            Assert.True(filterResult.IsSuccess);
            Assert.NotNull(filterResult.Data);
            Assert.Equal(1, filterResult.Data.TotalItems);
            Assert.Equal(1, filterResult.Data.TotalPages);
            Assert.Equal(4, Assert.Single(filterResult.Data.Items).Id);
            userBotService.VerifyAll();
        }

        [Fact]
        public async Task GetAllScrapes_WhenUserDoesNotOwnBot_ShouldReturnNotFound()
        {
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            using var cancellation = new CancellationTokenSource();
            var userBotService = new Mock<IUserBotService>(MockBehavior.Strict);
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, 1, cancellation.Token))
                .ReturnsAsync(false);
            var handler = new GetAllScrapesQueryHandler(dbContext, userBotService.Object);

            var result = await handler.Handle(new GetAllScrapesQuery(userId, 1), cancellation.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("User or bot not found", result.Message);
            Assert.Null(result.Data);
            userBotService.VerifyAll();
        }

        [Fact]
        public async Task GetAllScrapes_WhenNoScrapesExist_ShouldReturnEmptyPage()
        {
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var userBotService = new Mock<IUserBotService>();
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, 1, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllScrapesQueryHandler(dbContext, userBotService.Object);

            var result = await handler.Handle(new GetAllScrapesQuery(userId, 1), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Items);
            Assert.Equal(0, result.Data.TotalItems);
            Assert.Equal(0, result.Data.TotalPages);
            Assert.Equal(1, result.Data.CurrentPage);
        }

        [Fact]
        public async Task GetScrapeById_WhenBelongsToUser_ShouldReturnScrape()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var scrape = new Scrape
            {
                Id = 10,
                UserId = userId,
                BotId = 1,
                GroupId = 5,
                AccountId = 2,
                Status = ScrapeStatus.RUNNING.ToDbString(),
                CollectedLeadsCount = 12,
                StartedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            await dbContext.Scrapes.AddAsync(scrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetScrapeByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetScrapeByIdQuery(userId, 10), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(10, result.Data.Id);
            Assert.Equal(12, result.Data.CollectedLeadsCount);
        }

        [Fact]
        public async Task GetScrapeById_WhenBelongsToAnotherUser_ShouldReturnNotFound()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();

            var scrape = new Scrape { Id = 10, UserId = user2Id, BotId = 1, Status = ScrapeStatus.RUNNING.ToDbString() };
            await dbContext.Scrapes.AddAsync(scrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetScrapeByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetScrapeByIdQuery(user1Id, 10), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task CancelScrape_WhenRunning_ShouldMarkCancelledAndRevertBusyAccount()
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
            var scrape = new Scrape
            {
                Id = 50,
                UserId = userId,
                BotId = 1,
                AccountId = 100,
                Status = ScrapeStatus.RUNNING.ToDbString(),
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Scrapes.AddAsync(scrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new CancelScrapeCommandHandler(dbContext);

            // Act
            var result = await handler.Handle(new CancelScrapeCommand(userId, 50), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            var updatedScrape = await dbContext.Scrapes.FindAsync(50);
            Assert.Equal(ScrapeStatus.CANCELLED.ToDbString(), updatedScrape!.Status);
            Assert.NotNull(updatedScrape.EndedAt);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(100);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount!.Status);
        }

        [Fact]
        public async Task CancelScrape_WhenNotRunning_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var scrape = new Scrape
            {
                Id = 51,
                UserId = userId,
                BotId = 1,
                Status = ScrapeStatus.COMPLETED.ToDbString(),
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                EndedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            await dbContext.Scrapes.AddAsync(scrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new CancelScrapeCommandHandler(dbContext);

            // Act
            var result = await handler.Handle(new CancelScrapeCommand(userId, 51), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("Cannot cancel scrape with status 'Completed'. Only running scrapes can be cancelled.", result.Message);
        }
    }
}
