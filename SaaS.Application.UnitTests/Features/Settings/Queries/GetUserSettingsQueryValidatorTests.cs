using FluentValidation.TestHelper;
using SaaS.Application.Features.Settings.Queries;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Settings.Queries
{
    public class GetUserSettingsQueryValidatorTests
    {
        private readonly GetUserSettingsQueryValidator _validator;

        public GetUserSettingsQueryValidatorTests()
        {
            _validator = new GetUserSettingsQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetUserSettingsQuery(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetUserSettingsQuery(Guid.Empty);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
