using Microsoft.AspNetCore.Http;
using SaaS.Application.Common.Interfaces;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
namespace SaaS.Infrastructure.DataSeeding
{
    public static class BotsSeeding
    {
        public static async Task SeedingAsync(IAppDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var AllBots = new[]
            {
                new Bot
                {
                    Name = "Facebook Group Members Scraper",
                    Description = "Scrapes members from Facebook groups.",
                    CurrentPrice = 0,
                    UiModuleCode = "FB-MEMBERS-SCRAPER",
                    CooldownMinutes = 180,
                    IsActive = true
                }, 
                new Bot
                {
                    Name = "Facebook Group Posts Scraper",
                    Description = "Scrapes posts from Facebook groups.",
                    CurrentPrice = 0,
                    UiModuleCode = "FB-POSTS-SCRAPER",
                    CooldownMinutes = 180,
                    IsActive = true
                },
                new Bot
                {
                    Name = "Facebook Group Members With Messages Scraper",
                    Description = "Scrapes members and messages from Facebook groups.",
                    CurrentPrice = 0,
                    UiModuleCode = "FB-MEMBERS-MESSAGES-SCRAPER",
                    CooldownMinutes = 180,
                    IsActive = true
                }
            };

            if (!context.Bots.Any())
            {
                foreach (var bot in AllBots)
                {
                    context.Bots.Add(bot);
                }
            }

            await context.SaveChangesAsync();
        }

    }
}
