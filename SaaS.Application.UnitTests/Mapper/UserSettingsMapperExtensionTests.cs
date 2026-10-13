using FluentAssertions;
using Moq;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Mapper
{
    public class UserSettingsMapperExtensionTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;

        public UserSettingsMapperExtensionTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
        }

        [Fact]
        public void ToDto_WhenSettingsIsNull_ReturnsDefaultDto()
        {
            // Arrange
            UserSetting nullSettings = null!;

            // Act
            var dto = nullSettings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeFalse();
            dto.MaskedAIApiKey.Should().BeEmpty();
            dto.HasScraperToken.Should().BeFalse();
            dto.MaskedScraperToken.Should().BeEmpty();
            dto.DailyLeadLimit.Should().Be(0);
            dto.CompanyName.Should().BeNull();
            dto.CompanyPitch.Should().BeNull();
        }

        [Fact]
        public void ToDto_WhenSettingsHasValidTokens_MapsPropertiesAndMasksKeys()
        {
            // Arrange
            var plainOpenAi = "sk-proj-super-secret-openai-token-9999";
            var plainApify = "apify_api_key_secret_value_8888";

            _encryptionServiceMock.Setup(e => e.Decrypt("enc_openai"))
                .Returns(plainOpenAi);
            _encryptionServiceMock.Setup(e => e.Decrypt("enc_apify"))
                .Returns(plainApify);

            var settings = new UserSetting
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                AIApiKeyEncrypted = "enc_openai",
                ScraperApiTokenEncrypted = "enc_apify",
                DailyMessageLimit = 250,
                CompanyName = "Example company",
                CompanyPitch = "We automate sales outreach."
            };

            // Act
            var dto = settings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeTrue();
            dto.MaskedAIApiKey.Should().EndWith("9999");
            dto.MaskedAIApiKey.Should().StartWith("***");
            dto.HasScraperToken.Should().BeTrue();
            dto.MaskedScraperToken.Should().EndWith("8888");
            dto.MaskedScraperToken.Should().StartWith("***");
            dto.DailyLeadLimit.Should().Be(250);
            dto.CompanyName.Should().Be("Example company");
            dto.CompanyPitch.Should().Be("We automate sales outreach.");
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("   ", "   ")]
        public void ToDto_WhenTokensAreNullOrWhitespace_ReturnsFalseFlagsAndEmptyMaskedStrings(string? scraperToken, string? aiKey)
        {
            // Arrange
            var settings = new UserSetting
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                ScraperApiTokenEncrypted = scraperToken,
                AIApiKeyEncrypted = aiKey,
                DailyMessageLimit = 75
            };

            // Act
            var dto = settings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeFalse();
            dto.MaskedAIApiKey.Should().BeEmpty();
            dto.HasScraperToken.Should().BeFalse();
            dto.MaskedScraperToken.Should().BeEmpty();
            dto.DailyLeadLimit.Should().Be(75);
        }

        [Fact]
        public void ToDto_WhenOnlyScraperApiTokenIsProvided_MapsCorrectly()
        {
            // Arrange
            _encryptionServiceMock.Setup(e => e.Decrypt("enc_token"))
                .Returns("my-secret-key-1234");

            var settings = new UserSetting
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                ScraperApiTokenEncrypted = "enc_token",
                AIApiKeyEncrypted = null,
                DailyMessageLimit = 100
            };

            // Act
            var dto = settings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeFalse();
            dto.MaskedAIApiKey.Should().BeEmpty();
            dto.HasScraperToken.Should().BeTrue();
            dto.MaskedScraperToken.Should().EndWith("1234");
            dto.DailyLeadLimit.Should().Be(100);
        }

        [Fact]
        public void ToDto_WhenOnlyAIApiKeyIsProvided_MapsCorrectly()
        {
            // Arrange
            _encryptionServiceMock.Setup(e => e.Decrypt("enc_ai"))
                .Returns("ai-secret-key-5678");

            var settings = new UserSetting
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                ScraperApiTokenEncrypted = null,
                AIApiKeyEncrypted = "enc_ai",
                DailyMessageLimit = 50
            };

            // Act
            var dto = settings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeTrue();
            dto.MaskedAIApiKey.Should().EndWith("5678");
            dto.HasScraperToken.Should().BeFalse();
            dto.MaskedScraperToken.Should().BeEmpty();
            dto.DailyLeadLimit.Should().Be(50);
        }

        [Fact]
        public void ToDto_WhenDecryptionThrowsException_ReturnsEmptyMaskedStrings()
        {
            // Arrange
            _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Failed decryption"));

            var settings = new UserSetting
            {
                Id = 1,
                UserId = Guid.NewGuid(),
                ScraperApiTokenEncrypted = "bad_token",
                AIApiKeyEncrypted = "bad_key",
                DailyMessageLimit = 100
            };

            // Act
            var dto = settings.ToDto(_encryptionServiceMock.Object);

            // Assert
            dto.Should().NotBeNull();
            dto.HasAIApiKey.Should().BeTrue();
            dto.MaskedAIApiKey.Should().BeEmpty();
            dto.HasScraperToken.Should().BeTrue();
            dto.MaskedScraperToken.Should().BeEmpty();
            dto.DailyLeadLimit.Should().Be(100);
        }
    }
}
