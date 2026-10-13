using FluentValidation;

namespace SaaS.Application.Features.TargetGroups.Queries.GetAll
{
    public class GetAllGroupsQueryValidator : AbstractValidator<GetAllGroupsQuery>
    {
        public GetAllGroupsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.BotId)
                .GreaterThan(0).WithMessage("BotId must be greater than 0.");
        }
    }
}
