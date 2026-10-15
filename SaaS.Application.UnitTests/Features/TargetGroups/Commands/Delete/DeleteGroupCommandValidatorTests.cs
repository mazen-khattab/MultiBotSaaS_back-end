using FluentValidation.TestHelper;
using SaaS.Application.Features.TargetGroups.Commands.Delete;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Commands.Delete
{
    public class DeleteGroupCommandValidatorTests
    {
        private readonly DeleteGroupCommandValidator _validator;

        public DeleteGroupCommandValidatorTests()
        {
            _validator = new DeleteGroupCommandValidator();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Validate_WhenIdIsGreaterThanZero_ShouldNotHaveValidationError(int validId)
        {
            // Arrange
            var command = new DeleteGroupCommand(Guid.NewGuid(), validId);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-25)]
        public void Validate_WhenIdIsZeroOrNegative_ShouldHaveValidationError(int invalidId)
        {
            // Arrange
            var command = new DeleteGroupCommand(Guid.NewGuid(), invalidId);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id)
                .WithErrorMessage("A valid group Id must be provided.");
        }

        [Fact]
        public void Validate_WhenUserIdentityIsEmpty_ShouldHaveValidationError()
        {
            var result = _validator.TestValidate(new DeleteGroupCommand(Guid.Empty, 1));
            result.ShouldHaveValidationErrorFor(command => command.UserId);
        }
    }
}
