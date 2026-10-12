using FluentValidation.TestHelper;
using SaaS.Application.Features.Auth.Queries.User.GetCurrentUser;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Queries.GetCurrentUser
{
    public class GetCurrentUserQueryValidatorTests
    {
        private readonly GetCurrentUserQueryValidator _validator;

        public GetCurrentUserQueryValidatorTests()
        {
            _validator = new GetCurrentUserQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetCurrentUserQuery(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetCurrentUserQuery(Guid.Empty);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
