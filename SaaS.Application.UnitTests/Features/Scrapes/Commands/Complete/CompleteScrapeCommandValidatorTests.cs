using FluentValidation.TestHelper;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.Scrapes.Commands.Complete;
using System.Collections.Generic;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Scrapes.Commands.Complete
{
    public class CompleteScrapeCommandValidatorTests
    {
        private readonly CompleteScrapeCommandValidator _validator;

        public CompleteScrapeCommandValidatorTests()
        {
            _validator = new CompleteScrapeCommandValidator();
        }

        [Fact]
        public void Validate_WhenLeadsIsNotNull_ShouldNotHaveValidationError()
        {
            // Arrange - empty list is valid
            var command = new CompleteScrapeCommand(1, new List<ScrapedLeadDto>());

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Leads);
        }

        [Fact]
        public void Validate_WhenLeadsHasElements_ShouldNotHaveValidationError()
        {
            // Arrange
            var leads = new List<ScrapedLeadDto>
            {
                new ScrapedLeadDto("ext-1", "alice", "Alice Smith", "hello", "{}")
            };
            var command = new CompleteScrapeCommand(1, leads);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Leads);
        }

        [Fact]
        public void Validate_WhenLeadsIsNull_ShouldHaveValidationError()
        {
            // Arrange
            var command = new CompleteScrapeCommand(1, null!);

            // Act
            var result = _validator.TestValidate(command);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Leads)
                .WithErrorMessage("Leads list must be provided (can be empty).");
        }
    }
}
