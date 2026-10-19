using FluentValidation.TestHelper;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible;
using SaaS.Domain.Enums;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetEligible
{
    public class GetEligibleConnectedAccountsQueryValidatorTests
    {
        private readonly GetEligibleConnectedAccountsQueryValidator _validator;

        public GetEligibleConnectedAccountsQueryValidatorTests()
        {
            _validator = new GetEligibleConnectedAccountsQueryValidator();
        }

        [Theory]
        [InlineData(AccountType.SCRAPING)]
        [InlineData(AccountType.MESSAGING)]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors(AccountType type)
        {
            // Arrange
            var query = new GetEligibleConnectedAccountsQuery(Guid.NewGuid(), 1, type);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTypeIsUndefined_ShouldHaveValidationError()
        {
            var query = new GetEligibleConnectedAccountsQuery(Guid.NewGuid(), 1, (AccountType)999);

            var result = _validator.TestValidate(query);

            result.ShouldHaveValidationErrorFor(x => x.Type);
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetEligibleConnectedAccountsQuery(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenBotIdIsZeroOrNegative_ShouldHaveValidationError(int invalidBotId)
        {
            // Arrange
            var query = new GetEligibleConnectedAccountsQuery(Guid.NewGuid(), invalidBotId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId);
        }
    }
}
