using FluentValidation;

namespace SaaS.Application.Features.TargetGroups.Queries.GetById
{
    public class GetGroupByIdQueryValidator : AbstractValidator<GetGroupByIdQuery>
    {
        public GetGroupByIdQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.BotId)
                .GreaterThan(0).WithMessage("BotId must be greater than 0.");

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Id is required.")
                .GreaterThan(0)
                .WithMessage("A valid group Id must be provided.");
        }
    }
}
