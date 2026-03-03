namespace FreightVis.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }

    public int Type { get; set; }    // 1..n (MVP)
    public int Status { get; set; }  // 1=Open, 2=InProgress, 3=Blocked, 4=Closed (MVP)

    public DateTimeOffset OccurredAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid ReportedByUserId { get; set; }
    public Guid? OwnerUserId { get; set; }

    public int ReferenceNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
