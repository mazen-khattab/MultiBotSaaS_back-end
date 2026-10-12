using FluentValidation.TestHelper;
using SaaS.Application.Features.Runs.Queries.GetRunById;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Runs.Queries.GetRunById
{
    public class GetRunByIdQueryValidatorTests
    {
        private readonly GetRunByIdQueryValidator _validator;

        public GetRunByIdQueryValidatorTests()
        {
            _validator = new GetRunByIdQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetRunByIdQuery(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetRunByIdQuery(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenRunIdIsZeroOrNegative_ShouldHaveValidationError(int invalidRunId)
        {
            // Arrange
            var query = new GetRunByIdQuery(Guid.NewGuid(), invalidRunId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RunId);
        }
    }
}
