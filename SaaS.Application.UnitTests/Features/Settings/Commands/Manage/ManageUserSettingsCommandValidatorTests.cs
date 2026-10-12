using FluentValidation.TestHelper;
using SaaS.Application.Features.Settings.Commands.Manage;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Settings.Commands.Manage
{
    public class ManageUserSettingsCommandValidatorTests
    {
        private readonly ManageUserSettingsCommandValidator _validator;

        public ManageUserSettingsCommandValidatorTests()
        {
            _validator = new ManageUserSettingsCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new ManageUserSettingsCommand(Guid.NewGuid(), null, null, 100);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new ManageUserSettingsCommand(Guid.Empty, null, null, 100);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        public void Validate_WhenDailyLeadLimitIsNegative_ShouldHaveValidationError(int invalidLimit)
        {
            // Arrange
            var command = new ManageUserSettingsCommand(Guid.NewGuid(), null, null, invalidLimit);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DailyLeadLimit);
        }

        [Fact]
        public void Validate_WhenDailyLeadLimitIsZero_ShouldNotHaveValidationError()
        {
            // Arrange
            var command = new ManageUserSettingsCommand(Guid.NewGuid(), null, null, 0);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DailyLeadLimit);
        }
    }
}
