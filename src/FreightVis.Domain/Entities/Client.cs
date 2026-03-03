namespace FreightVis.Domain.Entities;

public sealed class Client
{
    public Guid Id { get; set; }
    public Guid? ParentClientId { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
