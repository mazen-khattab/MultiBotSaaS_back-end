using FluentValidation.TestHelper;
using SaaS.Application.Features.Scrapes.Queries.GetScrapeById;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Queries.GetScrapeById
{
    public class GetScrapeByIdQueryValidatorTests
    {
        private readonly GetScrapeByIdQueryValidator _validator;

        public GetScrapeByIdQueryValidatorTests()
        {
            _validator = new GetScrapeByIdQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetScrapeByIdQuery(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetScrapeByIdQuery(Guid.Empty, 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenScrapeIdIsZeroOrNegative_ShouldHaveValidationError(int invalidScrapeId)
        {
            // Arrange
            var query = new GetScrapeByIdQuery(Guid.NewGuid(), invalidScrapeId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ScrapeId);
        }
    }
}
