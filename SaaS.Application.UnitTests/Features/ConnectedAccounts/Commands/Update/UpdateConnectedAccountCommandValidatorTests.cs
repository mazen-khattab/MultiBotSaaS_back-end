using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.ConnectedAccounts.Commands.Update;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Update
{
    public class UpdateConnectedAccountCommandValidatorTests
    {
        private readonly UpdateConnectedAccountCommandValidator _validator;

        [Theory]
        [InlineData("Scraping")]
        [InlineData("Messaging")]
        public void Validate_WhenTypeIsSupported_ShouldNotHaveValidationErrors(string type)
        {
            var dto = new UpdateConnectedAccountDto("Account", "Facebook", "{}", "Active", true, type!);
            var result = _validator.TestValidate(new UpdateConnectedAccountCommand(Guid.NewGuid(), 1, dto));

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
            var dto = new UpdateConnectedAccountDto("Account", "Facebook", "{}", "Active", true, type!);
            var result = _validator.TestValidate(new UpdateConnectedAccountCommand(Guid.NewGuid(), 1, dto));

            result.ShouldHaveValidationErrorFor(x => x.AccountDto.Type);
        }

        public UpdateConnectedAccountCommandValidatorTests()
        {
            _validator = new UpdateConnectedAccountCommandValidator();
        }

        [Fact]
        public void Validate_WhenIdIsGreaterThanZero_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "{\"session\":\"abc\"}", "Active", true, "Scraping");
            var command = new UpdateConnectedAccountCommand(Guid.NewGuid(), 1, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenIdIsZeroOrNegative_ShouldHaveValidationError(int invalidId)
        {
            // Arrange
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "cookies", "Active", true, "Scraping");
            var command = new UpdateConnectedAccountCommand(Guid.NewGuid(), invalidId, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "{\"session\":\"abc\"}", "Active", true, "Scraping");
            var command = new UpdateConnectedAccountCommand(Guid.Empty, 1, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
