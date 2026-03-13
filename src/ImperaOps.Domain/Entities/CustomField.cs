namespace ImperaOps.Domain.Entities;

public sealed class CustomField : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// One of: text, textarea, number, currency, percentage, date, boolean,
    /// dropdown, multi_select, url, phone, email, rating
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>JSON array of option strings — used by dropdown and multi_select types.</summary>
    public string? Options { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
