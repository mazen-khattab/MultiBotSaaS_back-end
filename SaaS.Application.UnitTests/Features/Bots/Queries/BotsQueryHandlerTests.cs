using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Bots.Queries.GetAll;
using SaaS.Application.Features.Bots.Queries.GetById;
using SaaS.Application.Features.Bots.Queries.GetSubscribed;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Constants;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Bots.Queries
{
    public class BotsQueryHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"BotsDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task GetAllBots_ShouldReturnActiveBotsWithSubscriptionFlags()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var bot1 = new Bot
            {
                Id = 1,
                Name = "Members Scraper",
                Description = "Scrapes FB members",
                CurrentPrice = 29.99m,
                UiModuleCode = UiModuleCodes.FbMembersScraper,
                CooldownMinutes = 30,
                IsActive = true,
                Status = "Active"
            };
            var bot2 = new Bot
            {
                Id = 2,
                Name = "Posts Scraper",
                Description = "Scrapes FB posts",
                CurrentPrice = 49.99m,
                UiModuleCode = UiModuleCodes.FbPostsScraper,
                CooldownMinutes = 60,
                IsActive = true,
                Status = "Active"
            };
            var bot3 = new Bot
            {
                Id = 3,
                Name = "Inactive Bot",
                Description = "Inactive",
                CurrentPrice = 0,
                UiModuleCode = "TEST-BOT",
                CooldownMinutes = 0,
                IsActive = false,
                Status = "Disabled"
            };

            await dbContext.Bots.AddRangeAsync(bot1, bot2, bot3);
            await dbContext.UserBots.AddAsync(new UserBot
            {
                UserId = userId,
                BotId = 1,
                PurchaseDate = DateTime.UtcNow.AddDays(-10)
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetAllBotsQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetAllBotsQuery(userId), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.Count);

            var resultBot1 = result.Data.First(b => b.Id == 1);
            Assert.True(resultBot1.IsSubscribed);
            Assert.NotNull(resultBot1.SubscribedAt);
            Assert.Equal(UiModuleCodes.FbMembersScraper, resultBot1.UiModuleCode);

            var resultBot2 = result.Data.First(b => b.Id == 2);
            Assert.False(resultBot2.IsSubscribed);
            Assert.Null(resultBot2.SubscribedAt);
        }

        [Fact]
        public async Task GetSubscribedBots_ShouldReturnOnlyUserSubscribedBots()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var bot1 = new Bot { Id = 1, Name = "Bot 1", Description = "Desc 1", CurrentPrice = 10, UiModuleCode = UiModuleCodes.FbMembersScraper, IsActive = true, Status = "Active" };
            var bot2 = new Bot { Id = 2, Name = "Bot 2", Description = "Desc 2", CurrentPrice = 20, UiModuleCode = UiModuleCodes.FbPostsScraper, IsActive = true, Status = "Active" };

            await dbContext.Bots.AddRangeAsync(bot1, bot2);
            await dbContext.UserBots.AddAsync(new UserBot
            {
                UserId = userId,
                BotId = 2,
                PurchaseDate = DateTime.UtcNow.AddDays(-5)
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetSubscribedBotsQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetSubscribedBotsQuery(userId), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal(2, result.Data[0].Id);
            Assert.True(result.Data[0].IsSubscribed);
        }

        [Fact]
        public async Task GetBotById_WhenBotExists_ShouldReturnBotWithSubscriptionStatus()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();

            var bot = new Bot
            {
                Id = 1,
                Name = "Scraper",
                Description = "Desc",
                CurrentPrice = 19.99m,
                UiModuleCode = UiModuleCodes.FbMembersScraper,
                CooldownMinutes = 15,
                IsActive = true,
                Status = "Active"
            };
            await dbContext.Bots.AddAsync(bot);
            await dbContext.UserBots.AddAsync(new UserBot { UserId = userId, BotId = 1, PurchaseDate = DateTime.UtcNow });
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetBotByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetBotByIdQuery(userId, 1), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.Id);
            Assert.True(result.Data.IsSubscribed);
            Assert.Equal(UiModuleCodes.FbMembersScraper, result.Data.UiModuleCode);
        }

        [Fact]
        public async Task GetBotById_WhenBotNotFound_ShouldReturnNotFound()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var handler = new GetBotByIdQueryHandler(dbContext);

            // Act
            var result = await handler.Handle(new GetBotByIdQuery(Guid.NewGuid(), 999), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }
    }
}
