using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Commands;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class UpdateEventHandler : IRequestHandler<UpdateEventCommand, Unit>
{
    private readonly IEventRepository _repo;

    public UpdateEventHandler(IEventRepository repo) => _repo = repo;

    public async Task<Unit> Handle(UpdateEventCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(request.Id, ct);
        if (existing is null) return Unit.Value;

        existing.EventTypeId      = request.EventTypeId;
        existing.WorkflowStatusId = request.WorkflowStatusId;
        existing.Title            = request.Title;
        existing.OccurredAt       = request.OccurredAt;
        existing.Location         = request.Location;
        existing.Description      = request.Description;
        existing.OwnerUserId      = request.OwnerUserId;
        existing.RootCauseId      = request.RootCauseId;
        existing.CorrectiveAction = request.CorrectiveAction;
        existing.UpdatedAt        = DateTimeOffset.UtcNow;

        await _repo.UpdateAsync(existing, ct);
        return Unit.Value;
    }
}
