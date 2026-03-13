namespace ImperaOps.Domain.Entities;

public sealed class ReportSchedule
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>"weekly" or "monthly"</summary>
    public string Frequency { get; set; } = "weekly";
    /// <summary>0=Sunday .. 6=Saturday (used when Frequency is "weekly")</summary>
    public int DayOfWeek { get; set; } = 1;
    /// <summary>1-28 (used when Frequency is "monthly")</summary>
    public int DayOfMonth { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
