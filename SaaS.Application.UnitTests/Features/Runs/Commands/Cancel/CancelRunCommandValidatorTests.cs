using FluentValidation.TestHelper;
using SaaS.Application.Features.Runs.Commands.Cancel;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Runs.Commands.Cancel
{
    public class CancelRunCommandValidatorTests
    {
        private readonly CancelRunCommandValidator _validator;

        public CancelRunCommandValidatorTests()
        {
            _validator = new CancelRunCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new CancelRunCommand(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new CancelRunCommand(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenRunIdIsZeroOrNegative_ShouldHaveValidationError(int invalidRunId)
        {
            // Arrange
            var command = new CancelRunCommand(Guid.NewGuid(), invalidRunId);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RunId);
        }
    }
}
