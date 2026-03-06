namespace ImperaOps.Domain.Entities;

public sealed class ClientCounter : ISoftDeletable
{
    public long ClientId { get; set; }
    /// <summary>"event" | "task_{eventId}"</summary>
    public string CounterName { get; set; } = string.Empty;
    public long Value { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
