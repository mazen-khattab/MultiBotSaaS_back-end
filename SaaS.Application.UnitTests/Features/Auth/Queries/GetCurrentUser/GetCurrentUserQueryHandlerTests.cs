using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Auth.Queries.User.GetCurrentUser;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Queries.GetCurrentUser
{
    public class GetCurrentUserQueryHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"GetCurrentUserDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenUserNotFound_ShouldReturnUnauthorized()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var handler = new GetCurrentUserQueryHandler(dbContext);
            var query = new GetCurrentUserQuery(Guid.NewGuid());

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
            Assert.Equal("User not found or inactive.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenRegularUserExistsAndIsActive_ShouldReturnUserProfile()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                FullName = "John Doe",
                IsDeleted = false
            };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetCurrentUserQueryHandler(dbContext);
            var query = new GetCurrentUserQuery(userId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(userId.ToString(), result.Data.UserId);
            Assert.Equal("test@example.com", result.Data.Email);
            Assert.Equal("John Doe", result.Data.Name);
            Assert.Equal("User", result.Data.Role);
        }

        [Fact]
        public async Task Handle_WhenRegularUserIsDeleted_ShouldReturnUnauthorized()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "inactive@example.com",
                FullName = "Inactive User",
                IsDeleted = true
            };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetCurrentUserQueryHandler(dbContext);
            var query = new GetCurrentUserQuery(userId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        }
    }
}
