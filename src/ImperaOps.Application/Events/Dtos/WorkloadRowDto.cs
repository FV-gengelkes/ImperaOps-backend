namespace ImperaOps.Application.Events.Dtos;

public sealed record WorkloadRowDto(
    long? UserId,
    string UserName,
    long OpenEvents,
    long OpenTasks
);
