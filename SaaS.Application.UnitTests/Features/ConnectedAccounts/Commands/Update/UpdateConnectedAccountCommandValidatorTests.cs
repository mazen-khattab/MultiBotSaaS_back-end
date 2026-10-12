using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.ConnectedAccounts.Commands.Update;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Update
{
    public class UpdateConnectedAccountCommandValidatorTests
    {
        private readonly UpdateConnectedAccountCommandValidator _validator;

        public UpdateConnectedAccountCommandValidatorTests()
        {
            _validator = new UpdateConnectedAccountCommandValidator();
        }

        [Fact]
        public void Validate_WhenIdIsGreaterThanZero_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "{\"session\":\"abc\"}", "Active", true);
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
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "cookies", "Active", true);
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
            var dto = new UpdateConnectedAccountDto("Display Name", "Facebook", "{\"session\":\"abc\"}", "Active", true);
            var command = new UpdateConnectedAccountCommand(Guid.Empty, 1, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
