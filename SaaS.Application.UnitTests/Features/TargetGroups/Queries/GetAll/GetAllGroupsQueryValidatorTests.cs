using FluentValidation.TestHelper;
using SaaS.Application.Features.TargetGroups.Queries.GetAll;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Queries.GetAll
{
    public class GetAllGroupsQueryValidatorTests
    {
        private readonly GetAllGroupsQueryValidator _validator;

        public GetAllGroupsQueryValidatorTests()
        {
            _validator = new GetAllGroupsQueryValidator();
        }

        [Fact]
        public void Validate_WhenQueryIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var query = new GetAllGroupsQuery(Guid.NewGuid(), 1);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var query = new GetAllGroupsQuery(Guid.Empty, 1);

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
            var query = new GetAllGroupsQuery(Guid.NewGuid(), invalidBotId);

            // Act
            var result = _validator.TestValidate(query);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.BotId);
        }
    }
}
