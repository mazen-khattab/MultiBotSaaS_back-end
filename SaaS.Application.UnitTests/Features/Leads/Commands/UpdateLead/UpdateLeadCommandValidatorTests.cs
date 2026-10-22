using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.Leads.Commands.UpdateLead;

namespace SaaS.Application.UnitTests.Features.Leads.Commands.UpdateLead;

public class UpdateLeadCommandValidatorTests
{
    private readonly UpdateLeadCommandValidator _validator = new();
    private static UpdateLeadCommand ValidCommand() => new(Guid.NewGuid(), 3_000_000_001,
        new UpdateLeadDto("Test Lead", "https://example.test/profile", null, "Pending"));

    [Theory]
    [InlineData("Pending")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("pending")]
    [InlineData("COMPLETED")]
    public void SupportedStatusAndNullableAiMessage_AreAccepted(string status)
    {
        var command = ValidCommand();
        Assert.True(_validator.Validate(command with { LeadDto = command.LeadDto with { Status = status } }).IsValid);
    }

    [Fact]
    public void ColumnLengthBoundaries_AreAccepted()
    {
        var command = ValidCommand();
        const string urlPrefix = "https://example.test/";
        var dto = command.LeadDto with
        {
            ProfileName = new string('n', 255),
            ProfileUrl = urlPrefix + new string('p', 450 - urlPrefix.Length)
        };
        Assert.Equal(450, dto.ProfileUrl.Length);
        Assert.True(_validator.Validate(command with { LeadDto = dto }).IsValid);
    }

    [Fact]
    public void OverlongProfileFields_AreRejected()
    {
        var command = ValidCommand();
        var result = _validator.Validate(command with
        {
            LeadDto = command.LeadDto with
            {
                ProfileName = new string('n', 256),
                ProfileUrl = "https://example.test/" + new string('p', 431)
            }
        });
        Assert.Contains(result.Errors, error => error.PropertyName == "LeadDto.ProfileName");
        Assert.Contains(result.Errors, error => error.PropertyName == "LeadDto.ProfileUrl");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingProfileName_IsRejected(string? name)
    {
        var command = ValidCommand();
        Assert.False(_validator.Validate(command with { LeadDto = command.LeadDto with { ProfileName = name! } }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("profile/123")]
    [InlineData("https://")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test/profile")]
    public void InvalidProfileUrl_IsRejected(string? url)
    {
        var command = ValidCommand();
        Assert.False(_validator.Validate(command with { LeadDto = command.LeadDto with { ProfileUrl = url! } }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("Unknown")]
    public void MissingOrUnsupportedStatus_IsRejected(string? status)
    {
        var command = ValidCommand();
        Assert.False(_validator.Validate(command with { LeadDto = command.LeadDto with { Status = status! } }).IsValid);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void NonPositiveLeadId_IsRejected(long id)
        => Assert.False(_validator.Validate(ValidCommand() with { LeadId = id }).IsValid);

    [Fact]
    public void MissingUserId_IsRejected()
        => Assert.False(_validator.Validate(ValidCommand() with { UserId = Guid.Empty }).IsValid);

    [Fact]
    public void MissingDto_IsRejectedWithoutThrowing()
        => Assert.False(_validator.Validate(ValidCommand() with { LeadDto = null! }).IsValid);
}
