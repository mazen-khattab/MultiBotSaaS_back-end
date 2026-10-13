using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.Scrapes.Commands.Create;
using System;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Commands.Create
{
    public class CreateScrapeCommandValidatorTests
    {
        private readonly CreateScrapeCommandValidator _validator;

        public CreateScrapeCommandValidatorTests()
        {
            _validator = new CreateScrapeCommandValidator();
        }

        [Fact]
        public void Validate_WhenCommandIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var dto = new CreateScrapeDto(1, 10, 5, "{\"keyword\":\"developer\"}");
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var dto = new CreateScrapeDto(1, 10, null, "{}");
            var command = new CreateScrapeCommand(Guid.Empty, dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                .WithErrorMessage("A valid user Id must be provided.");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-5)]
        public void Validate_WhenBotIdIsZeroOrNegative_ShouldHaveValidationError(int invalidBotId)
        {
            // Arrange
            var dto = new CreateScrapeDto(invalidBotId, 10, null, "{}");
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CreateScrapeDto.BotId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public void Validate_WhenConnectedAccountIdIsZeroOrNegative_ShouldHaveValidationError(int invalidAccountId)
        {
            // Arrange
            var dto = new CreateScrapeDto(1, invalidAccountId, null, "{}");
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CreateScrapeDto.ConnectedAccountId)
                .WithErrorMessage("ConnectedAccountId is required.");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Validate_WhenInfoJsonIsNullOrEmpty_ShouldHaveValidationError(string? invalidJson)
        {
            // Arrange
            var dto = new CreateScrapeDto(1, 10, null, invalidJson!);
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CreateScrapeDto.InfoJson);
        }

        [Theory]
        [InlineData("not a json")]
        [InlineData("{invalid: json}")]
        [InlineData("{\"unclosed\": ")]
        public void Validate_WhenInfoJsonIsNotValidJson_ShouldHaveValidationError(string invalidJson)
        {
            // Arrange
            var dto = new CreateScrapeDto(1, 10, null, invalidJson);
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CreateScrapeDto.InfoJson)
                .WithErrorMessage("InfoJson must be valid JSON.");
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("{\"target\": \"group1\", \"limit\": 50}")]
        public void Validate_WhenInfoJsonIsValidJson_ShouldNotHaveValidationError(string validJson)
        {
            // Arrange
            var dto = new CreateScrapeDto(1, 10, null, validJson);
            var command = new CreateScrapeCommand(Guid.NewGuid(), dto);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CreateScrapeDto.InfoJson);
        }
    }
}
