namespace ImperaOps.Domain.Entities;

public sealed class RootCauseTaxonomyItem : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
