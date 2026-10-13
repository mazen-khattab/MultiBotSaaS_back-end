using FluentValidation.TestHelper;
using SaaS.Application.Features.Scrapes.Commands.Cancel;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Commands.Cancel
{
    public class CancelScrapeCommandValidatorTests
    {
        private readonly CancelScrapeCommandValidator _validator;

        public CancelScrapeCommandValidatorTests()
        {
            _validator = new CancelScrapeCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new CancelScrapeCommand(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new CancelScrapeCommand(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenScrapeIdIsZeroOrNegative_ShouldHaveValidationError(int invalidScrapeId)
        {
            // Arrange
            var command = new CancelScrapeCommand(Guid.NewGuid(), invalidScrapeId);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ScrapeId);
        }
    }
}
