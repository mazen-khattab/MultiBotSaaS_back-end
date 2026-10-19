using SaaS.Domain.Enums;

namespace SaaS.Domain.Extensions
{
    public static class AccountTypeExtension
    {
        public static string ToDbString(this AccountType type) => type switch
        {
            AccountType.SCRAPING => "Scraping",
            AccountType.MESSAGING => "Messaging",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped AccountType value.")
        };

        public static AccountType ParseFromDbToAccountType(this string dbValue) => dbValue switch
        {
            "Scraping" => AccountType.SCRAPING,
            "Messaging" => AccountType.MESSAGING,
            _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, "Unrecognized AccountType value from DB.")
        };
    }
}
