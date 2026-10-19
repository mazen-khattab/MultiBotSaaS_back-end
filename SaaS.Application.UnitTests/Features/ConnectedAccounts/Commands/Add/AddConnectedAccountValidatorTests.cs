using FluentAssertions;
using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.ConnectedAccounts.Commands.Add;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Add
{
    public class AddConnectedAccountValidatorTests
    {
        private readonly AddConnectedAccountValidator _validator;

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public void Validate_WhenTypeIsSupported_ShouldNotHaveValidationErrors(string type)
        {
            var dto = new AddConnectedAccountDto(1, "Account", "Facebook", "{}", type!);
            var result = _validator.TestValidate(new AddConnectedAccountCommand(Guid.NewGuid(), dto));

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Unknown")]
        [InlineData("scraping")]
        [InlineData("MESSAGING")]
        [InlineData(" Scraping ")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("999")]
        [InlineData("Scraping, Messaging")]
        public void Validate_WhenTypeIsUnsupported_ShouldHaveValidationError(string? type)
        {
            var dto = new AddConnectedAccountDto(1, "Account", "Facebook", "{}", type!);
            var result = _validator.TestValidate(new AddConnectedAccountCommand(Guid.NewGuid(), dto));

            result.ShouldHaveValidationErrorFor(x => x.AccountDto.Type);
        }

        public AddConnectedAccountValidatorTests()
        {
            _validator = new AddConnectedAccountValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var dto = new AddConnectedAccountDto(1, "Account 1", "Facebook", "{\"session\":\"abc\"}", "Scraping");
            var command = new AddConnectedAccountCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var dto = new AddConnectedAccountDto(1, "Account 1", "Facebook", "{\"session\":\"abc\"}", "Scraping");
            var command = new AddConnectedAccountCommand(Guid.Empty, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                .WithErrorMessage("A valid user Id must be provided.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenBotIdIsZeroOrNegative_ShouldHaveValidationError(int botId)
        {
            // Arrange
            var dto = new AddConnectedAccountDto(botId, "Account 1", "Facebook", "{\"session\":\"abc\"}", "Scraping");
            var command = new AddConnectedAccountCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AccountDto.BotId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WhenDisplayNameIsEmpty_ShouldHaveValidationError(string? displayName)
        {
            // Arrange
            var dto = new AddConnectedAccountDto(1, displayName!, "Facebook", "{\"session\":\"abc\"}", "Scraping");
            var command = new AddConnectedAccountCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AccountDto.DisplayName)
                .WithErrorMessage("DisplayName is required.");
        }

        [Theory]
        [InlineData("invalid-json")]
        [InlineData("{not-json}")]
        [InlineData("null-and-not-valid")]
        public void Validate_WhenCookiesAreInvalidJson_ShouldHaveValidationError(string invalidJson)
        {
            // Arrange
            var dto = new AddConnectedAccountDto(1, "Account 1", "Facebook", invalidJson, "Scraping");
            var command = new AddConnectedAccountCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AccountDto.CookiesPayload)
                .WithErrorMessage("CookiesPayload must be valid JSON.");
        }
    }
}
