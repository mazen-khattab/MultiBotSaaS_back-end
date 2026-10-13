using FluentValidation.TestHelper;
using SaaS.Application.Features.TargetGroups.Queries.GetById;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Queries.GetById
{
    public class GetGroupByIdQueryValidatorTests
    {
        private readonly GetGroupByIdQueryValidator _validator;

        public GetGroupByIdQueryValidatorTests()
        {
            _validator = new GetGroupByIdQueryValidator();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Validate_WhenQueryIsValid_ShouldNotHaveValidationError(int validId)
        {
            // Arrange
            var query = new GetGroupByIdQuery(Guid.NewGuid(), 1, validId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetGroupByIdQuery(Guid.Empty, 1, 10);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                .WithErrorMessage("A valid user Id must be provided.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenBotIdIsZeroOrNegative_ShouldHaveValidationError(int invalidBotId)
        {
            // Arrange
            var query = new GetGroupByIdQuery(Guid.NewGuid(), invalidBotId, 10);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId)
                .WithErrorMessage("BotId must be greater than 0.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-25)]
        public void Validate_WhenIdIsZeroOrNegative_ShouldHaveValidationError(int invalidId)
        {
            // Arrange
            var query = new GetGroupByIdQuery(Guid.NewGuid(), 1, invalidId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id)
                .WithErrorMessage("A valid group Id must be provided.");
        }
    }
}
