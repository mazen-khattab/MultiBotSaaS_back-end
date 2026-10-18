using MediatR;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.TargetGroups.Commands.UpdateCursor
{
    public record UpdateGroupCursorCommand(int Id, string Cursor) : IRequest<ApiResponse<bool>>;
}
