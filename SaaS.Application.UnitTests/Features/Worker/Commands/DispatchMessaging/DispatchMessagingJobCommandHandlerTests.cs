using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Worker.Commands.DispatchMessaging;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Worker.Commands.DispatchMessaging
{
    public class DispatchMessagingJobCommandHandlerTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly Mock<IUserBotService> _userBotServiceMock;
        private readonly Mock<INetworkClient> _networkClientMock;
        private readonly Mock<ILogger<DispatchMessagingJobCommandHandler>> _loggerMock;

        public DispatchMessagingJobCommandHandlerTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
            _userBotServiceMock = new Mock<IUserBotService>();
            _userBotServiceMock.Setup(service => service.CheckOwnershipAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _networkClientMock = new Mock<INetworkClient>();
            _loggerMock = new Mock<ILogger<DispatchMessagingJobCommandHandler>>();
        }

        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"DispatchMessagingDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        [Fact]
        public void Constructor_WhenContextIsNull_ShouldThrowArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DispatchMessagingJobCommandHandler(null!, _encryptionServiceMock.Object, _userBotServiceMock.Object, _networkClientMock.Object, _loggerMock.Object));
            Assert.Equal("dbContext", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenEncryptionServiceIsNull_ShouldThrowArgumentNullException()
        {
            using var dbContext = CreateDbContext();
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DispatchMessagingJobCommandHandler(dbContext, null!, _userBotServiceMock.Object, _networkClientMock.Object, _loggerMock.Object));
            Assert.Equal("encryptionService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenUserBotServiceIsNull_ShouldThrowArgumentNullException()
        {
            using var dbContext = CreateDbContext();
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DispatchMessagingJobCommandHandler(dbContext, _encryptionServiceMock.Object, null!, _networkClientMock.Object, _loggerMock.Object));
            Assert.Equal("userBotService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenNetworkClientIsNull_ShouldThrowArgumentNullException()
        {
            using var dbContext = CreateDbContext();
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DispatchMessagingJobCommandHandler(dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object, null!, _loggerMock.Object));
            Assert.Equal("networkClient", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
        {
            using var dbContext = CreateDbContext();
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DispatchMessagingJobCommandHandler(dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object, _networkClientMock.Object, null!));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public async Task Handle_WhenProcessingJobAlreadyExists_ShouldReturnTooManyRequestsFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            var existingJob = new Job
            {
                Id = 10,
                UserId = userId,
                BotId = botId,
                Type = JobType.MESSAGING.ToDbString(),
                Status = JobStatus.PROCESSING.ToDbString(),
                CreatedAt = DateTime.UtcNow
            };

            await dbContext.Jobs.AddAsync(existingJob);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, 5, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.TooManyRequests, result.ErrorType);
            Assert.Equal("A job is already processing for this bot. Please wait for it to finish.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenConnectedAccountNotFoundOrInactive_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, 999, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("No active connected account found for this bot.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenConnectedAccountStatusIsNotActive_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.BUSY.ToDbString()
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Contains("and cannot be used.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenCookiesAreExpired_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(-1) // Expired
                }
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("Your account cookies has been expired. Pls refresh it.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenOneOrMoreLeadsDoNotExistOrMismatch_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            // Only lead 1 exists, lead 2 does not
            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                Status = LeadStatus.PENDING.ToDbString()
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Leads.AddAsync(lead1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1, 2 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("One or more leads are invalid or do not belong to the user/bot.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenOneOrMoreLeadsAreNotPending_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                Status = LeadStatus.COMPLETED.ToDbString() // Not pending!
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Leads.AddAsync(lead1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("One or more leads are not in Pending state.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenAllConditionsMet_ShouldCreateJobAndDispatchSuccessfully()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                Status = LeadStatus.PENDING.ToDbString(),
                ProfileName = "Alice",
                ProfileUrl = "https://example.com/alice",
                AiMessage = "Hi"
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Leads.AddAsync(lead1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            _networkClientMock
                .Setup(n => n.PostJsonAsync("/dispatch-messaging", It.IsAny<object>(), ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NetworkResult.Ok(200, "{\"accepted\":true}"));

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.TotalLeadsCount);
            Assert.Equal("Job accepted successfully.", result.Message);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.BUSY.ToDbString(), updatedAccount.Status);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.UserId == userId);
            Assert.NotNull(job);
            Assert.Equal(JobStatus.PENDING.ToDbString(), job.Status);
            Assert.Equal(JobType.MESSAGING.ToDbString(), job.Type);
        }

        [Fact]
        public async Task Handle_WhenNetworkCallFails_ShouldCompensateAndReturnServerError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                Status = LeadStatus.PENDING.ToDbString()
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Leads.AddAsync(lead1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            _networkClientMock
                .Setup(n => n.PostJsonAsync("/dispatch-messaging", It.IsAny<object>(), ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NetworkResult.Fail(500, "Node worker crashed"));

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("Failed to reach worker server. Please try again later.", result.Message);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.UserId == userId);
            Assert.NotNull(job);
            Assert.Equal(JobStatus.FAILED.ToDbString(), job.Status);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount.Status);
        }

        [Fact]
        public async Task Handle_WhenNetworkCallTimesOut_ShouldCompensateAndReturnServerError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 5;

            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                IsActive = true,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(5)
                }
            };

            var lead1 = new Lead
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                Status = LeadStatus.PENDING.ToDbString()
            };

            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Leads.AddAsync(lead1);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _encryptionServiceMock.Setup(e => e.Decrypt("encrypted")).Returns("[{\"cookie\":1}]");

            _networkClientMock
                .Setup(n => n.PostJsonAsync("/dispatch-messaging", It.IsAny<object>(), ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var handler = new DispatchMessagingJobCommandHandler(
                dbContext, _encryptionServiceMock.Object, _userBotServiceMock.Object,
                _networkClientMock.Object, _loggerMock.Object);

            var command = new DispatchMessagingJobCommand(userId, botId, accountId, new List<long> { 1 });

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("The request timed out or was cancelled.", result.Message);

            var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.UserId == userId);
            Assert.NotNull(job);
            Assert.Equal(JobStatus.FAILED.ToDbString(), job.Status);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount.Status);
        }
    }
}
