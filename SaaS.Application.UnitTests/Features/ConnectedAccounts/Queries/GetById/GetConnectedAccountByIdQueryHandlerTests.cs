using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetById;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetById
{
    public class GetConnectedAccountByIdQueryHandlerTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;

        public GetConnectedAccountByIdQueryHandlerTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
        }

        private MockAppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenAccountExists_ReturnsDetailsDtoWithCountsAndMaskedCookies()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var accountId = 10;
            var userId = Guid.NewGuid();
            var expireDate = DateTime.UtcNow.AddDays(7);
            var plainCookies = "secret-cookie-data-1234";

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted-cookie-data"))
                .Returns(plainCookies);

            var botId = 1;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                DisplayName = "Main Facebook Account",
                Platform = "Facebook",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted-cookie-data",
                    CookiesExpireDate = expireDate
                },
                Leads = new List<Lead>
                {
                    new Lead { Id = 1, GroupId = 1, ProfileName = "Lead 1", UserId = userId },
                    new Lead { Id = 2, GroupId = 1, ProfileName = "Lead 2", UserId = userId }
                },
                Runs = new List<Run>
                {
                    new Run { Id = 1, GroupId = 1, UserId = userId, Status = "Completed" }
                }
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetConnectedAccountByIdQueryHandler(dbContext, _encryptionServiceMock.Object);
            var query = new GetConnectedAccountByIdQuery(userId, botId, accountId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Message.Should().Be("Connected account has been retrieved successfully");
            result.Data.Should().NotBeNull();
            result.Data.Id.Should().Be(accountId);
            result.Data.DisplayName.Should().Be("Main Facebook Account");
            result.Data.Platform.Should().Be("Facebook");
            result.Data.IsActive.Should().BeTrue();
            result.Data.ExpiredAt.Should().Be(expireDate);
            result.Data.RelatedLeadsCount.Should().Be(2);
            result.Data.RunsCount.Should().Be(1);
            result.Data.MaskedCookies.Should().EndWith("1234");
        }

        [Fact]
        public async Task Handle_WhenAccountNotFound_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var handler = new GetConnectedAccountByIdQueryHandler(dbContext, _encryptionServiceMock.Object);
            var query = new GetConnectedAccountByIdQuery(Guid.NewGuid(), 1, 999);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
            result.Message.Should().Be("Connected account with ID 999 not found");
        }

        [Fact]
        public async Task Handle_WhenAccountBelongsToDifferentUser_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var accountId = 10;
            var ownerUserId = Guid.NewGuid();
            var attackerUserId = Guid.NewGuid();
            var botId = 1;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = ownerUserId,
                BotId = botId,
                DisplayName = "Target Account",
                Platform = "Facebook",
                IsActive = true
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetConnectedAccountByIdQueryHandler(dbContext, _encryptionServiceMock.Object);
            var query = new GetConnectedAccountByIdQuery(attackerUserId, botId, accountId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
        }

        [Fact]
        public async Task Handle_WhenAccountBelongsToDifferentBot_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var accountId = 10;
            var userId = Guid.NewGuid();
            var botId = 1;
            var requestedBotId = 2;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                DisplayName = "Target Account",
                Platform = "Facebook",
                IsActive = true
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetConnectedAccountByIdQueryHandler(dbContext, _encryptionServiceMock.Object);
            var query = new GetConnectedAccountByIdQuery(userId, requestedBotId, accountId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
        }
    }
}
