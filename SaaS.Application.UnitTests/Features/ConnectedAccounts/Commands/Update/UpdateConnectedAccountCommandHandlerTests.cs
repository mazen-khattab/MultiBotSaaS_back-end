using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Features.ConnectedAccounts.Commands.Update;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Update
{
    public class UpdateConnectedAccountCommandHandlerTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;

        public UpdateConnectedAccountCommandHandlerTests()
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

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public async Task Handle_WhenAccountExistsAndCookiesProvided_UpdatesAccountAndCookies(string type)
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var account = new ConnectedAccount
            {
                UserId = userId,
                DisplayName = "Old Display Name",
                Type = type == "Scraping" ? "Messaging" : "Scraping",
                Platform = "Facebook",
                IsActive = false,
                Cookie = new ConnectedAccountCookie
                {
                    EncryptedCookies = "old-encrypted-cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(1)
                }
            };
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var newRawCookies = "{\"new_cookie\":\"val\"}";
            var newEncryptedCookies = "new-encrypted-val";
            _encryptionServiceMock.Setup(e => e.Encrypt(newRawCookies))
                .Returns(newEncryptedCookies);

            var handler = new UpdateConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object);
            var dto = new UpdateConnectedAccountDto("New Display Name", "Instagram", newRawCookies, "Active", true, type);
            var command = new UpdateConnectedAccountCommand(userId, account.Id, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(userId);
            result.Message.Should().Be("Connected account has been updated successfully");

            var updatedAccount = await dbContext.ConnectedAccounts
                .Include(a => a.Cookie)
                .FirstOrDefaultAsync(a => a.Id == account.Id);

            updatedAccount.Should().NotBeNull();
            updatedAccount!.DisplayName.Should().Be("New Display Name");
            updatedAccount.Platform.Should().Be("Instagram");
            updatedAccount.Type.Should().Be(type);
            updatedAccount.IsActive.Should().BeTrue();
            updatedAccount.Cookie.EncryptedCookies.Should().Be(newEncryptedCookies);
            updatedAccount.Cookie.CookiesExpireDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_WhenAccountExistsAndCookiesEmpty_UpdatesFieldsWithoutModifyingCookies(string? emptyCookies)
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var originalExpiry = DateTime.UtcNow.AddDays(3);
            var account = new ConnectedAccount
            {
                UserId = userId,
                DisplayName = "Old Name",
                Platform = "Facebook",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    EncryptedCookies = "existing-encrypted-cookie",
                    CookiesExpireDate = originalExpiry
                }
            };
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new UpdateConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object);
            var dto = new UpdateConnectedAccountDto("Updated Name", "LinkedIn", emptyCookies!, "Active", false, "Scraping");
            var command = new UpdateConnectedAccountCommand(userId, account.Id, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _encryptionServiceMock.Verify(e => e.Encrypt(It.IsAny<string>()), Times.Never);

            var updatedAccount = await dbContext.ConnectedAccounts
                .Include(a => a.Cookie)
                .FirstOrDefaultAsync(a => a.Id == account.Id);

            updatedAccount.Should().NotBeNull();
            updatedAccount!.DisplayName.Should().Be("Updated Name");
            updatedAccount.Platform.Should().Be("LinkedIn");
            updatedAccount.IsActive.Should().BeFalse();
            updatedAccount.Cookie.EncryptedCookies.Should().Be("existing-encrypted-cookie");
            updatedAccount.Cookie.CookiesExpireDate.Should().Be(originalExpiry);
        }

        [Fact]
        public async Task Handle_WhenAccountNotFound_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var handler = new UpdateConnectedAccountCommandHandler(dbContext, _encryptionServiceMock.Object);
            var dto = new UpdateConnectedAccountDto("Name", "Platform", "cookies", "Active", true, "Scraping");
            var command = new UpdateConnectedAccountCommand(Guid.NewGuid(), 999, dto);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
            result.Message.Should().Be("Connected account not found.");
        }
    }
}
