using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using MediatR;

namespace ImperaOps.Application.Tasks;

public sealed class CreateTaskHandler : IRequestHandler<CreateTaskCommand, CreateTaskResult>
{
    private readonly IEventRepository _eventRepo;
    private readonly ICounterService _counter;

    public CreateTaskHandler(IEventRepository eventRepo, ICounterService counter)
    {
        _eventRepo = eventRepo;
        _counter   = counter;
    }

    public async Task<CreateTaskResult> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var counterName = $"task_{request.EventId}";
        var refNumber   = await _counter.AllocateAsync(request.ClientId, counterName, ct);
        var publicId    = $"TSK-{refNumber:D4}";

        var maxOrder = await _eventRepo.GetMaxTaskSortOrderAsync(request.EventId, ct);

        var task = new EventTask
        {
            ClientId          = request.ClientId,
            EventId           = request.EventId,
            PublicId          = publicId,
            Title             = request.Title,
            Description       = request.Description,
            AssignedToUserId  = request.AssignedToUserId,
            DueAt             = request.DueAt,
            IsComplete        = false,
            SortOrder         = maxOrder + 1,
            CreatedAt         = now,
            UpdatedAt         = now,
        };

        var id = await _eventRepo.CreateTaskAsync(task, ct);
        return new CreateTaskResult(id, publicId);
    }
}

public sealed class UpdateTaskHandler : IRequestHandler<UpdateTaskCommand, Unit>
{
    private readonly IEventRepository _eventRepo;

    public UpdateTaskHandler(IEventRepository eventRepo) => _eventRepo = eventRepo;

    public async Task<Unit> Handle(UpdateTaskCommand request, CancellationToken ct)
    {
        var task = await _eventRepo.GetTaskByIdAsync(request.Id, ct);
        if (task is null) return Unit.Value;

        task.Title            = request.Title;
        task.Description      = request.Description;
        task.AssignedToUserId = request.AssignedToUserId;
        task.DueAt            = request.DueAt;
        task.UpdatedAt        = DateTimeOffset.UtcNow;

        await _eventRepo.UpdateTaskAsync(task, ct);
        return Unit.Value;
    }
}

public sealed class CompleteTaskHandler : IRequestHandler<CompleteTaskCommand, Unit>
{
    private readonly IEventRepository _eventRepo;

    public CompleteTaskHandler(IEventRepository eventRepo) => _eventRepo = eventRepo;

    public async Task<Unit> Handle(CompleteTaskCommand request, CancellationToken ct)
    {
        var task = await _eventRepo.GetTaskByIdAsync(request.Id, ct);
        if (task is null) return Unit.Value;

        task.IsComplete = true;
        task.UpdatedAt  = DateTimeOffset.UtcNow;

        await _eventRepo.UpdateTaskAsync(task, ct);
        return Unit.Value;
    }
}

public sealed class UncompleteTaskHandler : IRequestHandler<UncompleteTaskCommand, Unit>
{
    private readonly IEventRepository _eventRepo;

    public UncompleteTaskHandler(IEventRepository eventRepo) => _eventRepo = eventRepo;

    public async Task<Unit> Handle(UncompleteTaskCommand request, CancellationToken ct)
    {
        var task = await _eventRepo.GetTaskByIdAsync(request.Id, ct);
        if (task is null) return Unit.Value;

        task.IsComplete = false;
        task.UpdatedAt  = DateTimeOffset.UtcNow;

        await _eventRepo.UpdateTaskAsync(task, ct);
        return Unit.Value;
    }
}

public sealed class DeleteTaskHandler : IRequestHandler<DeleteTaskCommand, Unit>
{
    private readonly IEventRepository _eventRepo;

    public DeleteTaskHandler(IEventRepository eventRepo) => _eventRepo = eventRepo;

    public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        await _eventRepo.DeleteTaskAsync(request.Id, ct);
        return Unit.Value;
    }
}
