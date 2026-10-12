using FluentValidation.TestHelper;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Auth.Queries.GetCurrentAdmin
{
    public class GetCurrentAdminQueryValidatorTests
    {
        private readonly GetCurrentAdminQueryValidator _validator;

        public GetCurrentAdminQueryValidatorTests()
        {
            _validator = new GetCurrentAdminQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetCurrentAdminQuery(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenAdminIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetCurrentAdminQuery(Guid.Empty);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AdminId);
        }
    }
}
