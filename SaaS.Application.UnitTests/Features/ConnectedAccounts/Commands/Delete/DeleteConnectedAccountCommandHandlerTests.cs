using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Features.ConnectedAccounts.Commands.Delete;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Delete
{
    public class DeleteConnectedAccountCommandHandlerTests
    {
        private MockAppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenAccountExists_DeletesAccountAndReturnsSuccess()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var account = new ConnectedAccount
            {
                UserId = userId,
                DisplayName = "To Be Deleted",
                Platform = "Facebook",
                IsActive = true
            };
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new DeleteConnectedAccountCommandHandler(dbContext);
            var command = new DeleteConnectedAccountCommand(userId, account.Id);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(account.Id.ToString());
            result.Message.Should().Be("Account has been deleted successfully");

            var deletedAccount = await dbContext.ConnectedAccounts.FindAsync(account.Id);
            deletedAccount.Should().BeNull();
        }

        [Fact]
        public async Task Handle_WhenAccountNotFound_ReturnsNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var handler = new DeleteConnectedAccountCommandHandler(dbContext);
            var command = new DeleteConnectedAccountCommand(Guid.NewGuid(), 999);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorType.Should().Be(ErrorType.NotFound);
            result.Message.Should().Be("Account not found");
        }
    }
}
