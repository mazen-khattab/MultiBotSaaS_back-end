using FluentAssertions;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Domain.UnitTests.Extensions;

public class AccountTypeExtensionTests
{
    [Theory]
    [InlineData(AccountType.SCRAPING, "Scraping")]
    [InlineData(AccountType.MESSAGING, "Messaging")]
    public void AccountType_ConvertsToAndFromDbValue(AccountType type, string dbValue)
    {
        type.ToDbString().Should().Be(dbValue);
        dbValue.ParseFromDbToAccountType().Should().Be(type);
    }

    [Fact]
    public void ToDbString_UndefinedEnum_Throws()
    {
        var act = () => ((AccountType)999).ToDbString();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("type");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown")]
    [InlineData("scraping")]
    [InlineData("MESSAGING")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("999")]
    [InlineData("Scraping, Messaging")]
    public void ParseFromDb_InvalidValue_Throws(string? dbValue)
    {
        var act = () => dbValue!.ParseFromDbToAccountType();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("dbValue");
    }
}
