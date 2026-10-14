using FluentValidation.TestHelper;
using SaaS.Application.Features.Scrapes.Queries.GetAllScrapes;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Queries.GetAllScrapes
{
    public class GetAllScrapesQueryValidatorTests
    {
        private readonly GetAllScrapesQueryValidator _validator;

        public GetAllScrapesQueryValidatorTests()
        {
            _validator = new GetAllScrapesQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetAllScrapesQuery(Guid.NewGuid(), 1, 1, 10);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        //[Fact]
        //public void Validate_WhenBotIdIsNull_ShouldNotHaveValidationErrorForBotId()
        //{
        //    // Arrange
        //    var query = new GetAllScrapesQuery(Guid.NewGuid(), null, 1, 10);

        //    // Act
        //    var result = _validator.TestValidate(query);

        //    // Assert
        //    result.ShouldNotHaveValidationErrorFor(x => x.BotId);
        //}

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetAllScrapesQuery(Guid.Empty, 1, 1, 10);

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
            var query = new GetAllScrapesQuery(Guid.NewGuid(), invalidBotId, 1, 10);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenPageNumberIsLessThanOne_ShouldHaveValidationError(int invalidPageNumber)
        {
            // Arrange
            var query = new GetAllScrapesQuery(Guid.NewGuid(), 1, invalidPageNumber, 10);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageNumber);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void Validate_WhenPageSizeIsOutOfRange_ShouldHaveValidationError(int invalidPageSize)
        {
            // Arrange
            var query = new GetAllScrapesQuery(Guid.NewGuid(), 1, 1, invalidPageSize);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageSize);
        }
    }
}
