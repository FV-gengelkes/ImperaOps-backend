namespace ImperaOps.Domain.Entities;

public sealed class UserClientAccess : ISoftDeletable
{
    public long UserId { get; set; }
    public long ClientId { get; set; }
    public string Role { get; set; } = "Member"; // "Admin" | "Member"
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
