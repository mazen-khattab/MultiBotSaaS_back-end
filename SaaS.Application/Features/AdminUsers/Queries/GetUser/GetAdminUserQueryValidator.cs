using FluentValidation;

namespace SaaS.Application.Features.AdminUsers.Queries.GetUser;

public sealed class GetAdminUserQueryValidator : AbstractValidator<GetAdminUserQuery>
{
    public GetAdminUserQueryValidator() => RuleFor(query => query.UserId).NotEmpty();
}
