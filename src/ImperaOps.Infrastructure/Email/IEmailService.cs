namespace ImperaOps.Infrastructure.Email;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct = default);
    Task SendInviteAsync(string toEmail, string displayName, string setPasswordUrl, CancellationToken ct = default);
    Task SendEventAssignedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string eventUrl, CancellationToken ct = default);
    Task SendTaskAssignedAsync(string toEmail, string toName, string actorName, string taskTitle, string eventPublicId, string eventUrl, CancellationToken ct = default);
    Task SendCommentAddedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string commentSnippet, string eventUrl, CancellationToken ct = default);
    Task SendStatusChangedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string newStatusName, string eventUrl, CancellationToken ct = default);
    Task SendTaskDueReminderAsync(string toEmail, string displayName, string taskTitle, string eventPublicId, string eventTitle, DateTimeOffset dueAt, bool isOverdue, string eventUrl, CancellationToken ct = default);
}
