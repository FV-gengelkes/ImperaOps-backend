using Hangfire.Dashboard;

namespace ImperaOps.Api;

/// <summary>
/// Allows local requests unconditionally in dev.
/// In production, requires X-Dashboard-Key header matching the configured value.
/// </summary>
public sealed class HangfireDashboardFilter : IDashboardAuthorizationFilter
{
    private readonly string? _key;
    private readonly bool    _isDev;

    public HangfireDashboardFilter(string? key, bool isDev)
    {
        _key   = key;
        _isDev = isDev;
    }

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        // Always allow local access in development
        if (_isDev)
        {
            var addr = http.Connection.RemoteIpAddress;
            if (addr is not null &&
                (addr.ToString() == "127.0.0.1"
                 || addr.ToString() == "::1"
                 || System.Net.IPAddress.IsLoopback(addr)))
                return true;
        }

        // In production (or non-local dev): require X-Dashboard-Key header
        if (!string.IsNullOrWhiteSpace(_key))
        {
            if (http.Request.Headers.TryGetValue("X-Dashboard-Key", out var provided)
                && provided == _key)
                return true;
        }

        return false;
    }
}
