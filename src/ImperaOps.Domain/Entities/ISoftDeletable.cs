namespace ImperaOps.Domain.Entities;

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
