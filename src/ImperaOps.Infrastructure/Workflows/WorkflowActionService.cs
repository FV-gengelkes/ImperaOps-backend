using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Tasks;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowActionService : IWorkflowActionService
{
    private readonly ImperaOpsDbContext _db;
    private readonly IMediator _mediator;
    private readonly IAuditService _audit;

    public WorkflowActionService(ImperaOpsDbContext db, IMediator mediator, IAuditService audit)
    {
        _db = db;
        _mediator = mediator;
        _audit = audit;
    }

    public async Task CreateTaskAsync(long clientId, long eventId, string title, string? description,
        long? assignedToUserId, int? dueDaysFromNow, string ruleName, CancellationToken ct)
    {
        DateTimeOffset? dueAt = dueDaysFromNow.HasValue
            ? DateTimeOffset.UtcNow.AddDays(dueDaysFromNow.Value)
            : null;

        await _mediator.Send(new CreateTaskCommand(
            clientId, eventId, title, description, assignedToUserId, dueAt), ct);

        _audit.Record("event", eventId, clientId, "task_added",
            $"Task \"{title}\" created by workflow rule \"{ruleName}\".");
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddCommentAsync(long eventId, long clientId, string body, CancellationToken ct)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            EntityType = "event",
            EntityId = eventId,
            ClientId = clientId,
            EventType = "comment",
            UserId = null,
            UserDisplayName = "Workflow Automation",
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<long> GetNextRoundRobinUserAsync(long workflowRuleId, long[] userIds, CancellationToken ct)
    {
        var state = await _db.RoundRobinStates
            .FirstOrDefaultAsync(s => s.WorkflowRuleId == workflowRuleId, ct);

        int nextIndex;
        if (state == null)
        {
            nextIndex = 0;
            _db.RoundRobinStates.Add(new RoundRobinState
            {
                WorkflowRuleId = workflowRuleId,
                LastAssignedIndex = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            nextIndex = (state.LastAssignedIndex + 1) % userIds.Length;
            state.LastAssignedIndex = nextIndex;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return userIds[nextIndex];
    }
}
