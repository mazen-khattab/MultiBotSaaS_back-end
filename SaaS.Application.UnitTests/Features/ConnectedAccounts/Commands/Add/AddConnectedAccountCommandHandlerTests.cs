using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Features.ConnectedAccounts.Commands.Add;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Add
{
    public class AddConnectedAccountCommandHandlerTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly Mock<IUserBotService> _userBotServiceMock;

        public AddConnectedAccountCommandHandlerTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
            _userBotServiceMock = new Mock<IUserBotService>();
        }

        private MockAppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MockAppDbContext(options);
        }

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public async Task Handle_WhenBotOwnershipExists_CreatesAccountAndReturnsSuccess(string type)
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var botId = 10;
            var plainCookies = "{\"cookie\":\"session123\"}";
            var encryptedCookies = "enc-session123";

            _encryptionServiceMock.Setup(e => e.Encrypt(plainCookies))
                .Returns(encryptedCookies);

            _userBotServiceMock.Setup(u => u.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new AddConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object);
            var dto = new AddConnectedAccountDto(botId, "Marketing Account", "Facebook", plainCookies, type);
            var command = new AddConnectedAccountCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(userId);
            result.Message.Should().Be("Connected account has been added successfully");

            var savedAccount = await dbContext.ConnectedAccounts
                .Include(a => a.Cookie)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.BotId == botId);

            savedAccount.Should().NotBeNull();
            savedAccount!.DisplayName.Should().Be("Marketing Account");
            savedAccount.Platform.Should().Be("Facebook");
            savedAccount.Type.Should().Be(type);
            savedAccount.IsActive.Should().BeTrue();
            savedAccount.Cookie.Should().NotBeNull();
            savedAccount.Cookie.EncryptedCookies.Should().Be(encryptedCookies);
            savedAccount.Cookie.CookiesExpireDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Handle_WhenBotOwnershipDoesNotExist_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var botId = 99;

            _userBotServiceMock.Setup(u => u.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var handler = new AddConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object);
            var dto = new AddConnectedAccountDto(botId, "Account", "Facebook", "{\"cookie\":\"abc\"}", "Scraping");
            var command = new AddConnectedAccountCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
            result.Message.Should().Be("User or bot not found");

            var accountsInDb = await dbContext.ConnectedAccounts.CountAsync();
            accountsInDb.Should().Be(0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_WhenCookiesAreEmptyOrWhitespace_LeavesEncryptedCookiesEmpty(string? emptyCookies)
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var botId = 5;

            _userBotServiceMock.Setup(u => u.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new AddConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object);
            var dto = new AddConnectedAccountDto(botId, "No Cookie Account", "LinkedIn", emptyCookies!, "Scraping");
            var command = new AddConnectedAccountCommand(userId, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _encryptionServiceMock.Verify(e => e.Encrypt(It.IsAny<string>()), Times.Never);

            var savedAccount = await dbContext.ConnectedAccounts
                .Include(a => a.Cookie)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            savedAccount.Should().NotBeNull();
            savedAccount!.Cookie.EncryptedCookies.Should().BeEmpty();
        }
    }
}
