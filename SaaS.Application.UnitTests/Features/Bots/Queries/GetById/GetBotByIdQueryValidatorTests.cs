using FluentValidation.TestHelper;
using SaaS.Application.Features.Bots.Queries.GetById;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Bots.Queries.GetById
{
    public class GetBotByIdQueryValidatorTests
    {
        private readonly GetBotByIdQueryValidator _validator;

        public GetBotByIdQueryValidatorTests()
        {
            _validator = new GetBotByIdQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetBotByIdQuery(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetBotByIdQuery(Guid.Empty, 1);

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
            var query = new GetBotByIdQuery(Guid.NewGuid(), invalidBotId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId);
        }
    }
}
