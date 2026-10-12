using FluentValidation.TestHelper;
using SaaS.Application.Features.ConnectedAccounts.Commands.Delete;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Commands.Delete
{
    public class DeleteConnectedAccountCommandValidatorTests
    {
        private readonly DeleteConnectedAccountCommandValidator _validator;

        public DeleteConnectedAccountCommandValidatorTests()
        {
            _validator = new DeleteConnectedAccountCommandValidator();
        }

        [Fact]
        public void Validate_WhenIdIsGreaterThanZero_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new DeleteConnectedAccountCommand(Guid.NewGuid(), 1);

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
            var command = new DeleteConnectedAccountCommand(Guid.NewGuid(), invalidId);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new DeleteConnectedAccountCommand(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
