using System;
using System.Text.RegularExpressions;

namespace SaaS.Domain.Constants
{
    /// <summary>
    /// Known seed codes and the format shared by dynamically created frontend modules.
    /// </summary>
    public static class UiModuleCodes
    {
        public const string FbMembersScraper = "FB-MEMBERS-SCRAPER";
        public const string FbPostsScraper = "FB-POSTS-SCRAPER";
        public const string FbMembersMessagesScraper = "FB-MEMBERS-MESSAGES-SCRAPER";

        public static string Normalize(string code) => code.Trim().ToUpperInvariant();

        public static bool IsValid(string? code) =>
            !string.IsNullOrWhiteSpace(code) && Normalize(code).Length <= 50 &&
            Regex.IsMatch(Normalize(code), @"\A[A-Z0-9]+(?:-[A-Z0-9]+)*\z", RegexOptions.CultureInvariant);
    }
}
