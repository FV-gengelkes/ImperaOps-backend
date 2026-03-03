namespace FreightVis.Domain.Entities;

public sealed class UserClientAccess
{
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public string Role { get; set; } = "Member"; // "Admin" | "Member"
    public DateTimeOffset GrantedAt { get; set; }
}
