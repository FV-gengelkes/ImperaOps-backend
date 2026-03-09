namespace ImperaOps.Application.Abstractions;

public interface IClientAdminService
{
    Task ApplyTemplateAsync(long clientId, string templateId, CancellationToken ct,
        bool seedDemoData = false, long? ownerUserId = null);

    Task<int> PurgeEventDataAsync(long clientId, CancellationToken ct);
}
