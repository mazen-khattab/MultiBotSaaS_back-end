using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.TargetGroups.Commands.Update;
using Xunit;

namespace SaaS.Application.UnitTests.Features.TargetGroups.Commands.Update
{
    public class UpdateGroupCommandValidatorTests
    {
        private readonly UpdateGroupCommandValidator _validator;

        public UpdateGroupCommandValidatorTests()
        {
            _validator = new UpdateGroupCommandValidator();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Validate_WhenIdIsGreaterThanZero_ShouldNotHaveValidationError(int validId)
        {
            // Arrange
            var dto = new UpdateGroupDto("Name", "https://example.com", "{}", true, null);
            var command = new UpdateGroupCommand(Guid.NewGuid(), validId, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-50)]
        public void Validate_WhenIdIsZeroOrNegative_ShouldHaveValidationError(int invalidId)
        {
            // Arrange
            var dto = new UpdateGroupDto("Name", "https://example.com", "{}", true, null);
            var command = new UpdateGroupCommand(Guid.NewGuid(), invalidId, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id)
                .WithErrorMessage("A valid group Id must be provided.");
        }

        [Fact]
        public void Validate_WhenUserIdentityIsEmpty_ShouldHaveValidationError()
        {
            var result = _validator.TestValidate(new UpdateGroupCommand(Guid.Empty, 1,
                new UpdateGroupDto("Name", "https://example.com", "{}", true, null)));
            result.ShouldHaveValidationErrorFor(command => command.UserId);
        }
    }
}
