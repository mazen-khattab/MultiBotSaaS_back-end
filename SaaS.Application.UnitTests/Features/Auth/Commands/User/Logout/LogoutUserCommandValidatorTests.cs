using FluentValidation.TestHelper;
using SaaS.Application.Features.Auth.Commands.User.Logout;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Commands.User.Logout
{
    public class LogoutUserCommandValidatorTests
    {
        private readonly LogoutUserCommandValidator _validator;

        public LogoutUserCommandValidatorTests()
        {
            _validator = new LogoutUserCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new LogoutUserCommand(Guid.NewGuid(), "some-refresh-token");

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new LogoutUserCommand(Guid.Empty, "some-refresh-token");

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
