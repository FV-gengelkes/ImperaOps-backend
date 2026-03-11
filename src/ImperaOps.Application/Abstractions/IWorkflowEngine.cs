using ImperaOps.Domain.Entities;

namespace ImperaOps.Application.Abstractions;

public interface IWorkflowEngine
{
    Task EvaluateAsync(string triggerType, Event ev, Event? previousSnapshot, CancellationToken ct = default);
}
