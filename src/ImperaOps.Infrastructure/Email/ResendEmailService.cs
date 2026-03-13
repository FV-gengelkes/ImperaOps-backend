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

    public async Task SendWorkflowRuleAsync(string toEmail, string toName, string ruleName, string message, string eventPublicId, string eventUrl, CancellationToken ct = default)
    {
        var msg = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"Workflow alert: {ruleName} — {eventPublicId}",
            HtmlBody = WorkflowRuleHtml(toName, ruleName, message, eventPublicId, eventUrl),
        };
        await _resend.EmailSendAsync(msg, ct);
    }

    public async Task SendScheduledReportAsync(
        string toEmail, string toName, string clientName, string frequency,
        int total, int open, int closed, double? avgResolutionDays, int slaBreached,
        IReadOnlyList<(string Name, long Count)> byType,
        IReadOnlyList<(string Location, long Count)> topLocations,
        string dashboardUrl, CancellationToken ct = default)
    {
        var period = frequency == "monthly" ? "Monthly" : "Weekly";
        var message = new EmailMessage
        {
            From     = _from,
            To       = { toEmail },
            Subject  = $"{clientName} — {period} Event Report",
            HtmlBody = ScheduledReportHtml(toName, clientName, period, total, open, closed, avgResolutionDays, slaBreached, byType, topLocations, dashboardUrl),
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

    private static string ScheduledReportHtml(
        string toName, string clientName, string period,
        int total, int open, int closed, double? avgResolutionDays, int slaBreached,
        IReadOnlyList<(string Name, long Count)> byType,
        IReadOnlyList<(string Location, long Count)> topLocations,
        string dashboardUrl)
    {
        var typeRows = new System.Text.StringBuilder();
        foreach (var (name, count) in byType)
            typeRows.Append($"""<tr><td style="padding:6px 12px;border-bottom:1px solid #e2e8f0;color:#475569;font-size:14px;">{System.Net.WebUtility.HtmlEncode(name)}</td><td style="padding:6px 12px;border-bottom:1px solid #e2e8f0;color:#0B1F3B;font-size:14px;font-weight:600;text-align:right;">{count}</td></tr>""");

        var locationRows = new System.Text.StringBuilder();
        foreach (var (location, count) in topLocations)
            locationRows.Append($"""<tr><td style="padding:6px 12px;border-bottom:1px solid #e2e8f0;color:#475569;font-size:14px;">{System.Net.WebUtility.HtmlEncode(location)}</td><td style="padding:6px 12px;border-bottom:1px solid #e2e8f0;color:#0B1F3B;font-size:14px;font-weight:600;text-align:right;">{count}</td></tr>""");

        var avgDays = avgResolutionDays.HasValue ? avgResolutionDays.Value.ToString("0.1") : "—";
        var slaSection = slaBreached > 0
            ? $"""<p style="margin:16px 0 0;padding:8px 12px;background:#FEF2F2;border-left:3px solid #DC2626;color:#DC2626;font-size:13px;border-radius:0 4px 4px 0;"><strong>{slaBreached}</strong> SLA breach{(slaBreached == 1 ? "" : "es")} this period</p>"""
            : "";

        return $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px;">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#0B1F3B;padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:bold;">ImperaOps</span>
                  <span style="color:#94a3b8;font-size:14px;margin-left:12px;">{period} Report</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="margin:0 0 8px;color:#0B1F3B;font-size:22px;">{System.Net.WebUtility.HtmlEncode(clientName)}</h2>
                  <p style="margin:0 0 24px;color:#64748b;font-size:14px;">{period} event summary for {System.Net.WebUtility.HtmlEncode(toName)}</p>

                  <!-- KPI row -->
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:24px;">
                    <tr>
                      <td style="text-align:center;padding:16px;background:#f8fafc;border-radius:8px 0 0 8px;border:1px solid #e2e8f0;">
                        <div style="font-size:28px;font-weight:700;color:#0B1F3B;">{total}</div>
                        <div style="font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;">Total</div>
                      </td>
                      <td style="text-align:center;padding:16px;background:#f8fafc;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;">
                        <div style="font-size:28px;font-weight:700;color:#2F80ED;">{open}</div>
                        <div style="font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;">Open</div>
                      </td>
                      <td style="text-align:center;padding:16px;background:#f8fafc;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;">
                        <div style="font-size:28px;font-weight:700;color:#16A34A;">{closed}</div>
                        <div style="font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;">Closed</div>
                      </td>
                      <td style="text-align:center;padding:16px;background:#f8fafc;border-radius:0 8px 8px 0;border:1px solid #e2e8f0;border-left:none;">
                        <div style="font-size:28px;font-weight:700;color:#0B1F3B;">{avgDays}</div>
                        <div style="font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:0.05em;">Avg Days</div>
                      </td>
                    </tr>
                  </table>

                  {(byType.Count > 0 ? $"""
                  <!-- By type -->
                  <h3 style="margin:0 0 8px;color:#0B1F3B;font-size:15px;font-weight:600;">By Event Type</h3>
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:24px;border:1px solid #e2e8f0;border-radius:6px;overflow:hidden;">
                    {typeRows}
                  </table>
                  """ : "")}

                  {(topLocations.Count > 0 ? $"""
                  <!-- Top locations -->
                  <h3 style="margin:0 0 8px;color:#0B1F3B;font-size:15px;font-weight:600;">Top Locations</h3>
                  <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:24px;border:1px solid #e2e8f0;border-radius:6px;overflow:hidden;">
                    {locationRows}
                  </table>
                  """ : "")}

                  {slaSection}

                  <div style="margin-top:24px;">
                    <a href="{dashboardUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Dashboard</a>
                  </div>

                  <p style="margin:24px 0 0;color:#94a3b8;font-size:12px;">You're receiving this because scheduled reports are enabled for your organization. Manage preferences in your notification settings.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string WorkflowRuleHtml(string toName, string ruleName, string message, string eventPublicId, string eventUrl) => $"""
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
                  <h2 style="margin:0 0 16px;color:#0B1F3B;font-size:22px;">Workflow Alert</h2>
                  <p style="margin:0 0 16px;color:#475569;font-size:15px;">Hi {toName},</p>
                  <p style="margin:0 0 8px;color:#475569;font-size:15px;">A workflow rule triggered on event <strong>{eventPublicId}</strong>.</p>
                  <p style="margin:0 0 8px;color:#0B1F3B;font-size:15px;font-weight:600;">{ruleName}</p>
                  <blockquote style="margin:0 0 24px;padding:12px 16px;background:#f8fafc;border-left:3px solid #F59E0B;color:#475569;font-size:14px;border-radius:0 4px 4px 0;">{message}</blockquote>
                  <a href="{eventUrl}" style="display:inline-block;padding:12px 28px;background:#2F80ED;color:#ffffff;font-size:15px;font-weight:600;border-radius:6px;text-decoration:none;">View Event</a>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
