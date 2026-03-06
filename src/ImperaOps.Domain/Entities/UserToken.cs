namespace ImperaOps.Domain.Entities;

public sealed class UserToken : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "PasswordReset" | "Invite" | "Session"
    public string? Description { get; set; }          // user-agent for Session tokens
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
