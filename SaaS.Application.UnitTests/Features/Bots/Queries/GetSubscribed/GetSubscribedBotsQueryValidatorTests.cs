using FluentValidation.TestHelper;
using SaaS.Application.Features.Bots.Queries.GetSubscribed;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Bots.Queries.GetSubscribed
{
    public class GetSubscribedBotsQueryValidatorTests
    {
        private readonly GetSubscribedBotsQueryValidator _validator;

        public GetSubscribedBotsQueryValidatorTests()
        {
            _validator = new GetSubscribedBotsQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetSubscribedBotsQuery(Guid.NewGuid());

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetSubscribedBotsQuery(Guid.Empty);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }
    }
}
