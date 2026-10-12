using FluentValidation.TestHelper;
using SaaS.Application.Features.Auth.Commands.Admin.Logout;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Commands.Admin.Logout
{
    public class LogoutAdminCommandValidatorTests
    {
        private readonly LogoutAdminCommandValidator _validator;

        public LogoutAdminCommandValidatorTests()
        {
            _validator = new LogoutAdminCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var command = new LogoutAdminCommand(Guid.NewGuid(), "some-refresh-token");

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenAdminIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var command = new LogoutAdminCommand(Guid.Empty, "some-refresh-token");

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AdminId);
        }
    }
}
