using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetEligible
{
    public class GetEligibleConnectedAccountsQueryHandlerTests
    {
        private readonly Mock<IUserBotService> _userBotServiceMock;

        public GetEligibleConnectedAccountsQueryHandlerTests()
        {
            _userBotServiceMock = new Mock<IUserBotService>();
        }

        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"EligibleAccountsDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Theory]
        [InlineData(AccountType.SCRAPING)]
        [InlineData(AccountType.MESSAGING)]
        public async Task Handle_WhenUserDoesNotOwnBot_ShouldReturnNotFound(AccountType type)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock.Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var handler = new GetEligibleConnectedAccountsQueryHandler(dbContext, _userBotServiceMock.Object);

            // Act
            var result = await handler.Handle(new GetEligibleConnectedAccountsQuery(userId, botId, type), CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Theory]
        [InlineData(AccountType.SCRAPING)]
        [InlineData(AccountType.MESSAGING)]
        public async Task Handle_WhenEligibleAccountsExist_ShouldReturnThem(AccountType type)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock.Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Eligible account
            var eligibleAccount = new ConnectedAccount
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                DisplayName = "Eligible Account",
                Type = type.ToDbString(),
                Platform = "Facebook",
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 1,
                    EncryptedCookies = "valid_cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7)
                }
            };

            // Ineligible because Status == Busy
            var busyAccount = new ConnectedAccount
            {
                Id = 2,
                UserId = userId,
                BotId = botId,
                DisplayName = "Busy Account",
                Type = type.ToDbString(),
                Platform = "Facebook",
                IsActive = true,
                Status = AccountStatus.BUSY.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 2,
                    EncryptedCookies = "valid_cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7)
                }
            };

            // Ineligible because IsActive == false
            var inactiveAccount = new ConnectedAccount
            {
                Id = 3,
                UserId = userId,
                BotId = botId,
                DisplayName = "Inactive Account",
                Type = type.ToDbString(),
                Platform = "Facebook",
                IsActive = false,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 3,
                    EncryptedCookies = "valid_cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7)
                }
            };

            // Ineligible because Cookie expired
            var expiredAccount = new ConnectedAccount
            {
                Id = 4,
                UserId = userId,
                BotId = botId,
                DisplayName = "Expired Account",
                Type = type.ToDbString(),
                Platform = "Facebook",
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 4,
                    EncryptedCookies = "expired_cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(-1)
                }
            };

            var otherTypeAccount = new ConnectedAccount
            {
                Id = 5,
                UserId = userId,
                BotId = botId,
                DisplayName = "Other Type Account",
                Type = (type == AccountType.SCRAPING ? AccountType.MESSAGING : AccountType.SCRAPING).ToDbString(),
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 5,
                    EncryptedCookies = "valid_cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7)
                }
            };

            await dbContext.ConnectedAccounts.AddRangeAsync(eligibleAccount, busyAccount, inactiveAccount, expiredAccount, otherTypeAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetEligibleConnectedAccountsQueryHandler(dbContext, _userBotServiceMock.Object);

            // Act
            var result = await handler.Handle(new GetEligibleConnectedAccountsQuery(userId, botId, type), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal(1, result.Data[0].Id);
            Assert.Equal("Eligible Account", result.Data[0].DisplayName);
            Assert.Equal(type.ToDbString(), result.Data[0].Type);
            Assert.True(result.Data[0].IsActive);
        }

        [Theory]
        [InlineData(AccountType.SCRAPING)]
        [InlineData(AccountType.MESSAGING)]
        public async Task Handle_WhenNoEligibleAccountsExist_ShouldReturnEmptyList(AccountType type)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock.Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new GetEligibleConnectedAccountsQueryHandler(dbContext, _userBotServiceMock.Object);

            // Act
            var result = await handler.Handle(new GetEligibleConnectedAccountsQuery(userId, botId, type), CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data);
        }
    }
}
