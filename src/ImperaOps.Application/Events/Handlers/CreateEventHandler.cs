using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Commands;
using ImperaOps.Domain.Entities;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class CreateEventHandler : IRequestHandler<CreateEventCommand, CreateEventResult>
{
    private readonly IEventRepository _repo;
    private readonly ICounterService _counter;

    public CreateEventHandler(IEventRepository repo, ICounterService counter)
    {
        _repo    = repo;
        _counter = counter;
    }

    public async Task<CreateEventResult> Handle(CreateEventCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var refNumber = await _counter.AllocateAsync(request.ClientId, "event", ct);
        var publicId  = $"EVT-{refNumber:D4}";

        var ev = new Event
        {
            ClientId          = request.ClientId,
            PublicId          = publicId,
            EventTypeId       = request.EventTypeId,
            WorkflowStatusId  = request.WorkflowStatusId,
            Title             = request.Title,
            OccurredAt        = request.OccurredAt,
            Location          = request.Location,
            Description       = request.Description,
            ReportedByUserId        = request.ReportedByUserId,
            ExternalReporterName    = request.ExternalReporterName,
            ExternalReporterContact = request.ExternalReporterContact,
            OwnerUserId       = null,
            ReferenceNumber   = refNumber,
            CreatedAt         = now,
            UpdatedAt         = now,
        };

        var id = await _repo.CreateAsync(ev, ct);
        return new CreateEventResult(id, publicId);
    }
}
