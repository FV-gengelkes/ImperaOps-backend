namespace ImperaOps.Application.Abstractions;

public interface ICounterService
{
    Task<long> AllocateAsync(long clientId, string counterName, CancellationToken ct);
}
