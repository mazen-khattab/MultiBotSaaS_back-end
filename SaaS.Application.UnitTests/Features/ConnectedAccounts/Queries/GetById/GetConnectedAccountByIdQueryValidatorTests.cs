using FluentValidation.TestHelper;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetById;
using Xunit;

namespace SaaS.Application.UnitTests.Features.ConnectedAccounts.Queries.GetById
{
    public class GetConnectedAccountByIdQueryValidatorTests
    {
        private readonly GetConnectedAccountByIdQueryValidator _validator;

        public GetConnectedAccountByIdQueryValidatorTests()
        {
            _validator = new GetConnectedAccountByIdQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetConnectedAccountByIdQuery(Guid.NewGuid(), 1, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetConnectedAccountByIdQuery(Guid.Empty, 1, 1);

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
            var query = new GetConnectedAccountByIdQuery(Guid.NewGuid(), invalidBotId, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenIdIsZeroOrNegative_ShouldHaveValidationError(int invalidId)
        {
            // Arrange
            var query = new GetConnectedAccountByIdQuery(Guid.NewGuid(), 1, invalidId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }
    }
}
