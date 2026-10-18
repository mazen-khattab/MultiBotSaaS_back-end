using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.TargetGroups.Commands.UpdateCursor
{
    public class UpdateGroupCursorCommandValidator : AbstractValidator<UpdateGroupCursorCommand>
    {
        public UpdateGroupCursorCommandValidator()
        {
            //RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");

            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithMessage("A valid group Id must be provided.");

            RuleFor(x => x.Cursor)
                .NotEmpty()
                .WithMessage("Cursor is required");
        }
    }
}
