using System;
using System.Collections.Generic;

namespace SaaS.Domain.Constants
{
    /// <summary>
    /// Centralized registry of valid UI Module Codes supported by the frontend bot registry.
    /// </summary>
    public static class UiModuleCodes
    {
        public const string FbMembersScraper = "FB-MEMBERS-SCRAPER";
        public const string FbPostsScraper = "FB-POSTS-SCRAPER";
        public const string FbMembersMessagesScraper = "FB-MEMBERS-MESSAGES-SCRAPER";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FbMembersScraper,
            FbPostsScraper,
            FbMembersMessagesScraper
        };

        public static bool IsValid(string? code) =>
            !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
    }
}
