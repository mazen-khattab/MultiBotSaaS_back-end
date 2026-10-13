using FluentAssertions;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using Xunit;

namespace SaaS.Domain.UnitTests.Extensions
{
    public class ScrapeStatusExtensionsTests
    {
        [Theory]
        [InlineData(ScrapeStatus.RUNNING, "Running")]
        [InlineData(ScrapeStatus.PENDING, "Pending")]
        [InlineData(ScrapeStatus.COMPLETED, "Completed")]
        [InlineData(ScrapeStatus.FAILED, "Failed")]
        public void ToDbString_ValidEnum_ReturnsExpectedDbString(ScrapeStatus status, string expectedDbString)
        {
            // Act
            var result = status.ToDbString();

            // Assert
            result.Should().Be(expectedDbString);
        }

        [Fact]
        public void ToDbString_UnmappedEnumValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var invalidStatus = (ScrapeStatus)999;

            // Act
            var act = () => invalidStatus.ToDbString();

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("status");
        }

        [Fact]
        public void ToDbString_AllEnumMembers_AreMapped()
        {
            // Ensure every enum member in ScrapeStatus has an explicit mapping in ToDbString
            foreach (ScrapeStatus status in Enum.GetValues<ScrapeStatus>())
            {
                var act = () => status.ToDbString();
                act.Should().NotThrow();
            }
        }

        [Theory]
        [InlineData("Running", ScrapeStatus.RUNNING)]
        [InlineData("Pending", ScrapeStatus.PENDING)]
        [InlineData("Completed", ScrapeStatus.COMPLETED)]
        [InlineData("Failed", ScrapeStatus.FAILED)]
        public void ParseFromDb_ValidDbString_ReturnsExpectedEnum(string dbValue, ScrapeStatus expectedStatus)
        {
            // Act
            var result = dbValue.ParseFromDbToScrapeStatus();

            // Assert
            result.Should().Be(expectedStatus);
        }

        [Theory]
        [InlineData("InvalidStatus")]
        [InlineData("RUNNING")]
        [InlineData("running")]
        [InlineData("completed")]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseFromDb_UnrecognizedString_ThrowsArgumentOutOfRangeException(string invalidDbValue)
        {
            // Act
            var act = () => invalidDbValue.ParseFromDbToScrapeStatus();

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("dbValue");
        }

        [Fact]
        public void ParseFromDb_NullValue_ThrowsArgumentOutOfRangeException()
        {
            string nullDbValue = null!;

            // Act
            var act = () => nullDbValue.ParseFromDbToScrapeStatus();

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("dbValue");
        }

        [Theory]
        [InlineData(ScrapeStatus.RUNNING)]
        [InlineData(ScrapeStatus.PENDING)]
        [InlineData(ScrapeStatus.COMPLETED)]
        [InlineData(ScrapeStatus.FAILED)]
        public void RoundTrip_StatusToDbStringAndBack_ReturnsOriginalStatus(ScrapeStatus originalStatus)
        {
            // Act
            var dbString = originalStatus.ToDbString();
            var parsedStatus = dbString.ParseFromDbToScrapeStatus();

            // Assert
            parsedStatus.Should().Be(originalStatus);
        }
    }
}
