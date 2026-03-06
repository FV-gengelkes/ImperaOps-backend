using Microsoft.Extensions.Configuration;
using Resend;

namespace ImperaOps.Infrastructure.Email;

public sealed class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string  _from;
    private readonly string  _baseUrl;

    public ResendEmailService(IResend resend, IConfiguration config)
    {
        _resend  = resend;
        _from    = config["Email:FromAddress"]  ?? "onboarding@resend.dev";
        _baseUrl = config["App:BaseUrl"]        ?? "http://localhost:3000";
    }

    public async Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From    = _from,
            To      = { toEmail },
            Subject = "Reset your ImperaOps password",
            HtmlBody = PasswordResetHtml(displayName, resetUrl),
        };

        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendInviteAsync(string toEmail, string displayName, string setPasswordUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From    = _from,
            To      = { toEmail },
            Subject = "You've been invited to ImperaOps",
            HtmlBody = InviteHtml(displayName, setPasswordUrl),
        };

        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendEventAssignedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string eventUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"{eventPublicId} assigned to you",
            HtmlBody = EventAssignedHtml(toName, actorName, eventPublicId, eventTitle, eventUrl),
        };
        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendTaskAssignedAsync(string toEmail, string toName, string actorName, string taskTitle, string eventPublicId, string eventUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"Task assigned: {taskTitle}",
            HtmlBody = TaskAssignedHtml(toName, actorName, taskTitle, eventPublicId, eventUrl),
        };
        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendCommentAddedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string commentSnippet, string eventUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"New comment on {eventPublicId}",
            HtmlBody = CommentAddedHtml(toName, actorName, eventPublicId, eventTitle, commentSnippet, eventUrl),
        };
        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendStatusChangedAsync(string toEmail, string toName, string actorName, string eventPublicId, string eventTitle, string newStatusName, string eventUrl, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"{eventPublicId} status changed to {newStatusName}",
            HtmlBody = StatusChangedHtml(toName, actorName, eventPublicId, eventTitle, newStatusName, eventUrl),
        };
        await _resend.EmailSendAsync(message, ct);
    }

    public async Task SendTaskDueReminderAsync(string toEmail, string displayName, string taskTitle, string eventPublicId, string eventTitle, DateTimeOffset dueAt, bool isOverdue, string eventUrl, CancellationToken ct = default)
    {
        var subject = isOverdue
            ? $"Overdue task: {taskTitle}"
            : dueAt < DateTimeOffset.UtcNow.AddHours(24)
                ? $"Task due today: {taskTitle}"
                : $"Task due tomorrow: {taskTitle}";

        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = subject,
            HtmlBody = TaskDueReminderHtml(displayName, taskTitle, eventPublicId, eventTitle, dueAt, isOverdue, eventUrl),
        };
        await _resend.EmailSendAsync(message, ct);
    }

    // ── HTML templates ────────────────────────────────────────────────────────

    private static string PasswordResetHtml(string displayName, string url) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">Reset your password</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {displayName},</p>
                  <p style="margin:0 0 24px;color:#475569;font-size:15px;">We received a request to reset your ImperaOps password. Click the button below to choose a new one.</p>
                  <a href="{url}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">Reset Password</a>
                  <p style="margin:24px 0 0;color:#94a3b8;font-size:13px;">This link expires in 1 hour. If you didn't request a password reset, you can safely ignore this email.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string InviteHtml(string displayName, string url) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">You've been invited!</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {displayName},</p>
                  <p style="margin:0 0 24px;color:#475569;font-size:15px;">Your ImperaOps account has been created. Click the button below to set your password and get started.</p>
                  <a href="{url}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">Set My Password</a>
                  <p style="margin:24px 0 0;color:#94a3b8;font-size:13px;">This link expires in 7 days.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string EventAssignedHtml(string toName, string actorName, string eventPublicId, string eventTitle, string eventUrl) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">Event assigned to you</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {toName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;"><strong>{actorName}</strong> assigned event <strong>{eventPublicId}</strong> to you.</p>
                  <p style="margin:0 0 24px;color:#475569;font-size:15px;">{eventTitle}</p>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string TaskAssignedHtml(string toName, string actorName, string taskTitle, string eventPublicId, string eventUrl) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">Task assigned to you</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {toName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;"><strong>{actorName}</strong> assigned a task to you on event <strong>{eventPublicId}</strong>.</p>
                  <p style="margin:0 0 24px;color:#475569;font-size:15px;">{taskTitle}</p>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string CommentAddedHtml(string toName, string actorName, string eventPublicId, string eventTitle, string commentSnippet, string eventUrl) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">New comment on {eventPublicId}</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {toName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;"><strong>{actorName}</strong> commented on event <strong>{eventPublicId}</strong> — {eventTitle}.</p>
                  <blockquote style="margin:0 0 24px;padding:12px 16px;background:#f8fafc;border-left:3px solid #2F80ED;color:#475569;font-size:14px;border-radius:0 4px 4px 0;">{commentSnippet}</blockquote>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string StatusChangedHtml(string toName, string actorName, string eventPublicId, string eventTitle, string newStatusName, string eventUrl) => $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">Status updated: {eventPublicId}</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {toName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;"><strong>{actorName}</strong> changed the status of <strong>{eventPublicId}</strong> — {eventTitle}.</p>
                  <p style="margin:0 0 24px;color:#475569;font-size:15px;">New status: <strong>{newStatusName}</strong></p>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string TaskDueReminderHtml(string displayName, string taskTitle, string eventPublicId, string eventTitle, DateTimeOffset dueAt, bool isOverdue, string eventUrl)
    {
        var heading  = isOverdue ? "Task overdue" : "Task due soon";
        var badgeColor = isOverdue ? "#DC2626" : "#F59E0B";
        var badgeText  = isOverdue ? "OVERDUE" : (dueAt < DateTimeOffset.UtcNow.AddHours(24) ? "DUE TODAY" : "DUE TOMORROW");
        var dueLine  = dueAt.ToString("dddd, MMMM d, yyyy");
        return $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">{heading}</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {displayName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;">
                    <span style="display:inline-block;padding:2px 10px;background:{badgeColor};color:#ffffff;font-size:12px;font-weight:700;border-radius:4px;letter-spacing:0.05em;">{badgeText}</span>
                  </p>
                  <p style="margin:8px 0 4px;color:#0B1F3B;font-size:16px;font-weight:600;">{taskTitle}</p>
                  <p style="margin:0 0 8px;color:#64748b;font-size:14px;">On event <strong>{eventPublicId}</strong> — {eventTitle}</p>
                  <p style="margin:0 0 24px;color:#64748b;font-size:14px;">Due: {dueLine}</p>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event →</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }
}
