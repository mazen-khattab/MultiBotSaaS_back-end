using FluentValidation;

namespace SaaS.Application.Features.Leads.Queries.GetLeadById
{
    public class GetLeadByIdQueryValidator : AbstractValidator<GetLeadByIdQuery>
    {
        public GetLeadByIdQueryValidator()
        {
            RuleFor(query => query.UserId).NotEmpty().WithMessage("UserId is required.");
            RuleFor(query => query.LeadId).GreaterThan(0).WithMessage("LeadId must be greater than 0.");
        }
    }
}
