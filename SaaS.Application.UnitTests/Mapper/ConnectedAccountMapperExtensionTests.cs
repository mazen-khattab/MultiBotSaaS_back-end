using FluentAssertions;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using Xunit;

namespace SaaS.Application.UnitTests.Mapper
{
    public class ConnectedAccountMapperExtensionTests
    {
        private readonly Mock<IEncryptionService> _encryptionServiceMock;

        public ConnectedAccountMapperExtensionTests()
        {
            _encryptionServiceMock = new Mock<IEncryptionService>();
        }

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public void ToDto_ValidAccount_MapsAllPropertiesCorrectly(string type)
        {
            // Arrange
            var expireDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var account = new ConnectedAccount
            {
                Id = 42,
                DisplayName = "Test Account",
                Type = type,
                Platform = "Facebook",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 42,
                    CookiesExpireDate = expireDate
                }
            };

            // Act
            var dto = account.ToDto();

            // Assert
            dto.Should().NotBeNull();
            dto.Id.Should().Be(42);
            dto.DisplayName.Should().Be("Test Account");
            dto.Platform.Should().Be("Facebook");
            dto.Type.Should().Be(type);
            dto.ExpiredAt.Should().Be(expireDate);
            dto.IsActive.Should().BeTrue();
        }

        [Fact]
        public void ToDto_NullAccount_ThrowsArgumentNullException()
        {
            // Arrange
            ConnectedAccount nullAccount = null!;

            // Act
            var act = () => nullAccount.ToDto();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("account");
        }

        [Fact]
        public void ToDtoList_ValidAccounts_ReturnsMappedList()
        {
            // Arrange
            var expireDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var accounts = new List<ConnectedAccount>
            {
                new ConnectedAccount
                {
                    Id = 1,
                    DisplayName = "Account 1",
                    Platform = "Facebook",
                    IsActive = true,
                    Cookie = new ConnectedAccountCookie { AccountId = 1, CookiesExpireDate = expireDate }
                },
                new ConnectedAccount
                {
                    Id = 2,
                    DisplayName = "Account 2",
                    Platform = "Instagram",
                    IsActive = false,
                    Cookie = new ConnectedAccountCookie { AccountId = 2, CookiesExpireDate = expireDate.AddDays(1) }
                }
            };

            // Act
            var dtos = accounts.ToDtoList();

            // Assert
            dtos.Should().NotBeNull();
            dtos.Should().HaveCount(2);
            dtos[0].Id.Should().Be(1);
            dtos[0].DisplayName.Should().Be("Account 1");
            dtos[1].Id.Should().Be(2);
            dtos[1].DisplayName.Should().Be("Account 2");
        }

        [Fact]
        public void ToDtoList_EmptyCollection_ReturnsEmptyList()
        {
            // Arrange
            var accounts = new List<ConnectedAccount>();

            // Act
            var dtos = accounts.ToDtoList();

            // Assert
            dtos.Should().NotBeNull();
            dtos.Should().BeEmpty();
        }

        [Fact]
        public void ToDtoList_NullCollection_ThrowsArgumentNullException()
        {
            // Arrange
            IEnumerable<ConnectedAccount> nullAccounts = null!;

            // Act
            var act = () => nullAccounts.ToDtoList();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("accounts");
        }

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public void ToDetailsDto_ValidAccount_MapsAllPropertiesAndMasksCookies(string type)
        {
            // Arrange
            var expireDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var plainCookie = "my_secret_session_cookie_value";
            var encryptedCookie = "encrypted_blob";

            _encryptionServiceMock.Setup(e => e.Decrypt(encryptedCookie))
                .Returns(plainCookie);

            var account = new ConnectedAccount
            {
                Id = 10,
                DisplayName = "Details Account",
                Type = type,
                Platform = "Twitter",
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    AccountId = 10,
                    CookiesExpireDate = expireDate,
                    EncryptedCookies = encryptedCookie
                }
            };

            // Act
            var detailsDto = account.ToDetailsDto(_encryptionServiceMock.Object, relatedLeadsCount: 25, scrapesCount: 5);

            // Assert
            detailsDto.Should().NotBeNull();
            detailsDto.Id.Should().Be(10);
            detailsDto.DisplayName.Should().Be("Details Account");
            detailsDto.Platform.Should().Be("Twitter");
            detailsDto.Type.Should().Be(type);
            detailsDto.ExpiredAt.Should().Be(expireDate);
            detailsDto.IsActive.Should().BeTrue();
            detailsDto.RelatedLeadsCount.Should().Be(25);
            detailsDto.ScrapesCount.Should().Be(5);
            detailsDto.MaskedCookies.Should().EndWith("alue");
            detailsDto.MaskedCookies.Should().StartWith("***");
        }

        [Fact]
        public void ToDetailsDto_NullAccount_ThrowsArgumentNullException()
        {
            // Arrange
            ConnectedAccount nullAccount = null!;

            // Act
            var act = () => nullAccount.ToDetailsDto(_encryptionServiceMock.Object, 1, 1);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("account");
        }

        [Fact]
        public void ToDetailsDto_NegativeRelatedLeadsCount_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var account = new ConnectedAccount
            {
                Id = 1,
                Cookie = new ConnectedAccountCookie()
            };

            // Act
            var act = () => account.ToDetailsDto(_encryptionServiceMock.Object, relatedLeadsCount: -1, scrapesCount: 5);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("relatedLeadsCount");
        }

        [Fact]
        public void ToDetailsDto_NegativeScrapesCount_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var account = new ConnectedAccount
            {
                Id = 1,
                Cookie = new ConnectedAccountCookie()
            };

            // Act
            var act = () => account.ToDetailsDto(_encryptionServiceMock.Object, relatedLeadsCount: 5, scrapesCount: -1);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("scrapesCount");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ToDetailsDto_WhenEncryptedCookiesIsNullOrWhitespace_ReturnsEmptyMaskedCookies(string? encryptedCookie)
        {
            // Arrange
            var account = new ConnectedAccount
            {
                Id = 1,
                Cookie = new ConnectedAccountCookie
                {
                    EncryptedCookies = encryptedCookie!
                }
            };

            // Act
            var detailsDto = account.ToDetailsDto(_encryptionServiceMock.Object, 0, 0);

            // Assert
            detailsDto.MaskedCookies.Should().BeEmpty();
        }

        [Fact]
        public void ToDetailsDto_WhenDecryptionFails_ReturnsEmptyMaskedCookies()
        {
            // Arrange
            _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>()))
                .Throws(new Exception("Decryption failure"));

            var account = new ConnectedAccount
            {
                Id = 1,
                Cookie = new ConnectedAccountCookie
                {
                    EncryptedCookies = "corrupt_data"
                }
            };

            // Act
            var detailsDto = account.ToDetailsDto(_encryptionServiceMock.Object, 0, 0);

            // Assert
            detailsDto.MaskedCookies.Should().BeEmpty();
        }
    }
}
