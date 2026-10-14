using System.Linq.Expressions;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper;

public static class AdminSubscriptionMapperExtension
{
    // Row presence is the existing ownership state; UserBot has no inactive flag/history.
    public static readonly Expression<Func<UserBot, AdminSubscriptionDto>> Projection = assignment => new AdminSubscriptionDto(
        assignment.Id, assignment.UserId, assignment.User.FullName, assignment.User.Email,
        assignment.BotId, assignment.Bot.Name, assignment.Bot.UiModuleCode,
        assignment.PurchasePrice, assignment.PurchaseDate, true);
}
