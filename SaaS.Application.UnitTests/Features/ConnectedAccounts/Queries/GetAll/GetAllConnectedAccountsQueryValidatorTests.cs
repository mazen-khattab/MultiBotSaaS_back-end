using FluentValidation.TestHelper;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetAll;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetAll
{
    public class GetAllConnectedAccountsQueryValidatorTests
    {
        private readonly GetAllConnectedAccountsQueryValidator _validator;

        public GetAllConnectedAccountsQueryValidatorTests()
        {
            _validator = new GetAllConnectedAccountsQueryValidator();
        }

        [Fact]
        public void Validate_WhenUserIdIsValidGuid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetAllConnectedAccountsQuery(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetAllConnectedAccountsQuery(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                .WithErrorMessage("A valid user Id must be provided.");
        }
    }
}
