using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Queries.GetCurrentAdmin
{
    public class GetCurrentAdminQueryHandlerTests
    {
        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"GetCurrentAdminDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public async Task Handle_WhenAdminNotFound_ShouldReturnUnauthorized()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var handler = new GetCurrentAdminQueryHandler(dbContext);
            var query = new GetCurrentAdminQuery(Guid.NewGuid());

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
            Assert.Equal("Admin not found or inactive.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenAdminExistsAndIsActive_ShouldReturnAdminProfile()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var adminId = Guid.NewGuid();
            var admin = new SystemAdmin
            {
                Id = adminId,
                Email = "admin@example.com",
                FullName = "Admin User",
                Role = "SuperAdmin",
                IsActive = true
            };
            await dbContext.SystmeAdmins.AddAsync(admin);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetCurrentAdminQueryHandler(dbContext);
            var query = new GetCurrentAdminQuery(adminId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(adminId.ToString(), result.Data.UserId);
            Assert.Equal("admin@example.com", result.Data.Email);
            Assert.Equal("Admin User", result.Data.Name);
            Assert.Equal("SuperAdmin", result.Data.Role);
        }

        [Fact]
        public async Task Handle_WhenAdminExistsAndIsInactive_ShouldReturnUnauthorized()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var adminId = Guid.NewGuid();
            var admin = new SystemAdmin
            {
                Id = adminId,
                Email = "inactive_admin@example.com",
                FullName = "Inactive Admin",
                Role = "Admin",
                IsActive = false
            };
            await dbContext.SystmeAdmins.AddAsync(admin);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new GetCurrentAdminQueryHandler(dbContext);
            var query = new GetCurrentAdminQuery(adminId);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        }
    }
}
