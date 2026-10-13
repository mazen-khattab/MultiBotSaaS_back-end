using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Scrapes.Commands.Create;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Commands.Create
{
    public class CreateScrapeCommandHandlerTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly Mock<INetworkClient> _networkClientMock;
        private readonly Mock<IN8nWebhookResolver> _webhookResolverMock;
        private readonly Mock<IUserBotService> _userBotServiceMock;
        private readonly Mock<ILogger<CreateScrapeCommandHandler>> _loggerMock;

        public CreateScrapeCommandHandlerTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
            _networkClientMock = new Mock<INetworkClient>();
            _webhookResolverMock = new Mock<IN8nWebhookResolver>();
            _userBotServiceMock = new Mock<IUserBotService>();
            _loggerMock = new Mock<ILogger<CreateScrapeCommandHandler>>();
        }

        private MockAppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: $"CreateScrapeDb_{Guid.NewGuid()}")
                .Options;

            return new MockAppDbContext(options);
        }

        private void SetupValidMocks(int botId, string uiCode = "BOT_LEAD_GEN")
        {
            _encryptionServiceMock
                .Setup(e => e.Decrypt(It.IsAny<string>()))
                .Returns("[{\"name\":\"cookie1\",\"value\":\"val1\"}]");

            _webhookResolverMock
                .Setup(w => w.GetWebhookUrl(uiCode))
                .Returns("https://n8n.example.com/webhook/scrapes");

            _networkClientMock
                .Setup(n => n.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(), ExternalSystem.N8n, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NetworkResult.Ok(200, "{\"status\":\"ok\"}"));
        }

        [Fact]
        public async Task Handle_WhenUserDoesNotOwnBot_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, 1, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("User or bot not found", result.Message);
        }

        [Fact]
        public async Task Handle_WhenConnectedAccountNotFound_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, 999, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Connected account not found.", result.Message);
        }

        [Theory]
        [InlineData("Busy")]
        [InlineData("Cooling_Down")]
        [InlineData("Error")]
        public async Task Handle_WhenConnectedAccountIsNotActive_ShouldReturnValidationError(string invalidStatus)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT" };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = invalidStatus
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Contains($"Account is currently {invalidStatus} and cannot be used.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenPreviousScrapeIsStillInProgress_ShouldReturnTooManyRequestsFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 60 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString()
            };

            // Previous scrape with EndedAt == null (still running)
            var activeScrape = new Scrape
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                AccountId = accountId,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                EndedAt = null,
                Status = ScrapeStatus.RUNNING.ToDbString()
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Scrapes.AddAsync(activeScrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.TooManyRequests, result.ErrorType);
            Assert.Equal("The previous scrape is still in progress. Please wait until it completes.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenCooldownPeriodIsActive_ShouldReturnTooManyRequestsFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 60 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString()
            };

            // Previous scrape ended 10 minutes ago, cooldown is 60 minutes -> 50 minutes remaining
            var lastScrape = new Scrape
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                AccountId = accountId,
                StartedAt = DateTime.UtcNow.AddMinutes(-40),
                EndedAt = DateTime.UtcNow.AddMinutes(-10),
                Status = ScrapeStatus.COMPLETED.ToDbString()
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.Scrapes.AddAsync(lastScrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.TooManyRequests, result.ErrorType);
            Assert.Contains("Cooldown active. You can start a new scrape after", result.Message);
        }

        [Fact]
        public async Task Handle_WhenTargetGroupIdProvidedButNotFound_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;
            var nonExistentGroupId = 999;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString()
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, nonExistentGroupId, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Target group not found.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenUserSettingsNotFound_ShouldReturnNotFoundFailure()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString()
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Some user settings is missing.", result.Message);
        }

        [Theory]
        [InlineData(null, "Pitch")]
        [InlineData("", "Pitch")]
        [InlineData("Company", null)]
        [InlineData("Company", "")]
        public async Task Handle_WhenCompanyInfoIsIncomplete_ShouldReturnNotFoundFailure(string? companyName, string? companyPitch)
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString()
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = companyName,
                CompanyPitch = companyPitch
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("Some user settings is missing.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenCookiesHaveExpired_ShouldReturnValidationError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Test Bot", UiModuleCode = "UI_BOT", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "expired-cookie",
                    CookiesExpireDate = DateTime.UtcNow.AddMinutes(-5) // Expired
                }
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = "Tech Corp",
                CompanyPitch = "Our Pitch"
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal("Your account cookies has been expired. Pls refresh it.", result.Message);
        }

        [Fact]
        public async Task Handle_WhenAllConditionsMet_ShouldCreateScrapeAndDispatchExternalCallSuccessfully()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;
            var groupId = 25;

            var bot = new Bot { Id = botId, Name = "Lead Bot", UiModuleCode = "BOT_FACEBOOK", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted-secret",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(10)
                }
            };

            var targetGroup = new TargetGroup
            {
                Id = groupId,
                UserId = userId,
                BotId = botId,
                GroupName = "Dentists"
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = "Dentist Outreach Inc",
                CompanyPitch = "We provide dental leads"
            };

            // Previous scrape completed long ago, cooldown satisfied
            var previousScrape = new Scrape
            {
                Id = 1,
                UserId = userId,
                BotId = botId,
                AccountId = accountId,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                EndedAt = DateTime.UtcNow.AddHours(-1),
                Status = ScrapeStatus.COMPLETED.ToDbString()
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.TargetGroups.AddAsync(targetGroup);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.Scrapes.AddAsync(previousScrape);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            SetupValidMocks(botId, "BOT_FACEBOOK");

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, groupId, "{\"maxLeads\": 50}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.Data > 0);
            Assert.Equal("Scrape started successfully.", result.Message);

            var scrape = await dbContext.Scrapes.FindAsync(result.Data);
            Assert.NotNull(scrape);
            Assert.Equal(userId, scrape.UserId);
            Assert.Equal(botId, scrape.BotId);
            Assert.Equal(accountId, scrape.AccountId);
            Assert.Equal(groupId, scrape.GroupId);
            Assert.Equal(ScrapeStatus.RUNNING.ToDbString(), scrape.Status);
            Assert.Equal("{\"maxLeads\": 50}", scrape.InfoJson);

            // Account status should be BUSY
            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.BUSY.ToDbString(), updatedAccount.Status);

            _networkClientMock.Verify(n =>
                n.PostJsonAsync("https://n8n.example.com/webhook/scrapes", It.IsAny<object>(), ExternalSystem.N8n, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenTargetGroupIdIsNull_ShouldCreateScrapeWithoutGroupId()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Lead Bot", UiModuleCode = "BOT_FACEBOOK", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted-secret",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(10)
                }
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = "Dentist Outreach Inc",
                CompanyPitch = "We provide dental leads"
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            SetupValidMocks(botId, "BOT_FACEBOOK");

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            var scrape = await dbContext.Scrapes.FindAsync(result.Data);
            Assert.NotNull(scrape);
            Assert.Null(scrape.GroupId);
        }

        [Fact]
        public async Task Handle_WhenExternalCallReturnsFailure_ShouldCompensateAndReturnServerError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Lead Bot", UiModuleCode = "BOT_FACEBOOK", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted-secret",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(10)
                }
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = "Dentist Outreach Inc",
                CompanyPitch = "We provide dental leads"
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            SetupValidMocks(botId, "BOT_FACEBOOK");

            // Network client returns failure
            _networkClientMock
                .Setup(n => n.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(), ExternalSystem.N8n, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NetworkResult.Fail(500, "n8n server unavailable"));

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("Failed to dispatch scrape to external system.", result.Message);

            // Verify compensation: Scrape status is FAILED, Account status restored to ACTIVE
            var scrape = await dbContext.Scrapes.FirstOrDefaultAsync(r => r.UserId == userId);
            Assert.NotNull(scrape);
            Assert.Equal(ScrapeStatus.FAILED.ToDbString(), scrape.Status);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount.Status);
        }

        [Fact]
        public async Task Handle_WhenExternalCallThrowsException_ShouldCompensateAndReturnServerError()
        {
            // Arrange
            using var dbContext = CreateDbContext();
            var userId = Guid.NewGuid();
            var botId = 1;
            var accountId = 10;

            var bot = new Bot { Id = botId, Name = "Lead Bot", UiModuleCode = "BOT_FACEBOOK", CooldownMinutes = 10 };
            var account = new ConnectedAccount
            {
                Id = accountId,
                UserId = userId,
                BotId = botId,
                Bot = bot,
                Status = AccountStatus.ACTIVE.ToDbString(),
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = accountId,
                    EncryptedCookies = "encrypted-secret",
                    CookiesExpireDate = DateTime.UtcNow.AddDays(10)
                }
            };

            var userSetting = new UserSetting
            {
                Id = 1,
                UserId = userId,
                CompanyName = "Dentist Outreach Inc",
                CompanyPitch = "We provide dental leads"
            };

            await dbContext.Bots.AddAsync(bot);
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.UserSettings.AddAsync(userSetting);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            SetupValidMocks(botId, "BOT_FACEBOOK");

            // Network client throws exception (e.g. timeout / connection refused)
            _networkClientMock
                .Setup(n => n.PostJsonAsync(It.IsAny<string>(), It.IsAny<object>(), ExternalSystem.N8n, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("Connection refused"));

            _userBotServiceMock
                .Setup(s => s.CheckOwnershipAsync(userId, botId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new CreateScrapeCommandHandler(
                dbContext, _encryptionServiceMock.Object, _networkClientMock.Object,
                _webhookResolverMock.Object, _userBotServiceMock.Object, _loggerMock.Object);

            var command = new CreateScrapeCommand(userId, new CreateScrapeDto(botId, accountId, null, "{}"));

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.ServerError, result.ErrorType);
            Assert.Equal("Failed to dispatch scrape to external system.", result.Message);

            // Verify compensation
            var scrape = await dbContext.Scrapes.FirstOrDefaultAsync(r => r.UserId == userId);
            Assert.NotNull(scrape);
            Assert.Equal(ScrapeStatus.FAILED.ToDbString(), scrape.Status);

            var updatedAccount = await dbContext.ConnectedAccounts.FindAsync(accountId);
            Assert.NotNull(updatedAccount);
            Assert.Equal(AccountStatus.ACTIVE.ToDbString(), updatedAccount.Status);
        }
    }
}
