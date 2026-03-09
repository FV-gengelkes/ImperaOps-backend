using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class GetEventDetailHandler(IEventReadRepository readRepo)
    : IRequestHandler<GetEventDetailQuery, EventDetailDto?>
{
    public async Task<EventDetailDto?> Handle(GetEventDetailQuery request, CancellationToken ct)
    {
        var dto = await readRepo.GetByPublicIdAsync(request.PublicId, ct, request.ClientId);
        if (dto is null) return null;

        var slaRule = await readRepo.GetSlaRuleForEventAsync(dto.ClientId, dto.EventTypeId, ct);
        if (slaRule is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var createdAt = new DateTimeOffset(dto.CreatedAt, TimeSpan.Zero);

            DateTimeOffset? invDeadline = slaRule.InvestigationHours.HasValue
                ? createdAt.AddHours(slaRule.InvestigationHours.Value)
                : null;
            DateTimeOffset? closureDeadline = slaRule.ClosureHours.HasValue
                ? createdAt.AddHours(slaRule.ClosureHours.Value)
                : null;

            dto.Sla = new SlaStatusDto
            {
                RuleId                = slaRule.Id,
                RuleName              = slaRule.Name,
                InvestigationDeadline = invDeadline,
                InvestigationBreached = invDeadline.HasValue && dto.OwnerUserId == null && !dto.WorkflowStatusIsClosed && now > invDeadline.Value,
                ClosureDeadline       = closureDeadline,
                ClosureBreached       = closureDeadline.HasValue && !dto.WorkflowStatusIsClosed && now > closureDeadline.Value,
            };
        }

        return dto;
    }
}
