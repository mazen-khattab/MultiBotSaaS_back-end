using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Helpers;
using SaaS.Application.Common.Interfaces;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Mapper
{
    public static class ConnectedAccountMapperExtension
    {
        public static ConnectedAccountDto ToDto(this ConnectedAccount account)
        {
            ArgumentNullException.ThrowIfNull(account, nameof(account));

            return new ConnectedAccountDto
                (
                    Id: account.Id,
                    DisplayName: account.DisplayName,
                    Platform: account.Platform,
                    Type: account.Type,
                    ExpiredAt: account.Cookie.CookiesExpireDate,
                    IsActive: account.IsActive
                );
        }

        public static List<ConnectedAccountDto> ToDtoList(this IEnumerable<ConnectedAccount> accounts)
        {
            ArgumentNullException.ThrowIfNull(accounts, nameof(accounts));

            return [.. accounts.Select(account => account.ToDto())];
        }

        public static ConnectedAccountDetailsDto ToDetailsDto(this ConnectedAccount account, IEncryptionService encryptionService,int relatedLeadsCount, int scrapesCount)
        {
            ArgumentNullException.ThrowIfNull(account, nameof(account));
            ArgumentOutOfRangeException.ThrowIfNegative(relatedLeadsCount);
            ArgumentOutOfRangeException.ThrowIfNegative(scrapesCount);

            return new ConnectedAccountDetailsDto
            (
                Id: account.Id,
                DisplayName: account.DisplayName,
                MaskedCookies: Helper.MaskValue(account.Cookie.EncryptedCookies, encryptionService),
                Platform: account.Platform,
                Type: account.Type,
                ExpiredAt: account.Cookie.CookiesExpireDate,
                IsActive: account.IsActive,
                Status: account.Status,
                RelatedLeadsCount: relatedLeadsCount,
                ScrapesCount: scrapesCount
            );
        }
    }
}
