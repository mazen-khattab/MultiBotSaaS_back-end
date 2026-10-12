using FluentValidation.TestHelper;
using SaaS.Application.Features.Bots.Queries.GetAll;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Bots.Queries.GetAll
{
    public class GetAllBotsQueryValidatorTests
    {
        private readonly GetAllBotsQueryValidator _validator;

        public GetAllBotsQueryValidatorTests()
        {
            _validator = new GetAllBotsQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetAllBotsQuery(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetAllBotsQuery(Guid.Empty);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
