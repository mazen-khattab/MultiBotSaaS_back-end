using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetAll;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetAll
{
    public class GetAllConnectedAccountsQueryHandlerTests
    {
        private MockAppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenAccountsExistForUser_ReturnsMappedAccountList()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var targetUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var botId = 1;

            var account1 = new ConnectedAccount
            {
                UserId = targetUserId,
                BotId = botId,
                DisplayName = "User Account 1",
                Platform = "Facebook",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7)
                }
            };
            var account2 = new ConnectedAccount
            {
                UserId = targetUserId,
                BotId = botId,
                DisplayName = "User Account 2",
                Platform = "Instagram",
                IsActive = false,
                Cookie = new ConnectedAccountCookie
                {
                    CookiesExpireDate = DateTime.UtcNow.AddDays(14)
                }
            };
            var differentBotAccount = new ConnectedAccount
            {
                UserId = targetUserId,
                BotId = 2,
                DisplayName = "Different Bot Account",
                Platform = "Facebook",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    CookiesExpireDate = DateTime.UtcNow.AddDays(3)
                }
            };
            var otherUserAccount = new ConnectedAccount
            {
                UserId = otherUserId,
                BotId = botId,
                DisplayName = "Other User Account",
                Platform = "Twitter",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            await dbContext.ConnectedAccounts.AddRangeAsync(account1, account2, differentBotAccount, otherUserAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var userBotService = new Mock<IUserBotService>();
            userBotService.Setup(service => service.CheckOwnershipAsync(targetUserId, botId, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllConnectedAccountsQueryHandler(dbContext, userBotService.Object);
            var query = new GetAllConnectedAccountsQuery(targetUserId, botId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Be("Connected accounts have been retrieved successfully");
            result.Data.Should().NotBeNull();
            result.Data.Should().HaveCount(2);
            result.Data.Should().Contain(a => a.DisplayName == "User Account 1" && a.Platform == "Facebook");
            result.Data.Should().Contain(a => a.DisplayName == "User Account 2" && a.Platform == "Instagram");
            result.Data.Should().NotContain(a => a.DisplayName == "Different Bot Account");
            result.Data.Should().NotContain(a => a.DisplayName == "Other User Account");
        }

        [Fact]
        public async Task Handle_WhenNoAccountsExistForUser_ReturnsEmptyList()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userIdWithNoAccounts = Guid.NewGuid();

            var userBotService = new Mock<IUserBotService>();
            userBotService.Setup(service => service.CheckOwnershipAsync(userIdWithNoAccounts, 1, CancellationToken.None))
                .ReturnsAsync(true);
            var handler = new GetAllConnectedAccountsQueryHandler(dbContext, userBotService.Object);
            var query = new GetAllConnectedAccountsQuery(userIdWithNoAccounts, 1);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull().And.BeEmpty();
            result.Message.Should().Be("No connected accounts found");
        }

        [Fact]
        public async Task Handle_WhenUserDoesNotOwnBot_ReturnsNotFoundFailure()
        {
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            using var cancellation = new CancellationTokenSource();
            var userBotService = new Mock<IUserBotService>(MockBehavior.Strict);
            userBotService.Setup(service => service.CheckOwnershipAsync(userId, 1, cancellation.Token))
                .ReturnsAsync(false);
            var handler = new GetAllConnectedAccountsQueryHandler(dbContext, userBotService.Object);

            var result = await handler.Handle(new GetAllConnectedAccountsQuery(userId, 1), cancellation.Token);

            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
            result.Message.Should().Be("User or bot not found");
            result.Data.Should().BeNull();
            userBotService.VerifyAll();
        }
    }
}
