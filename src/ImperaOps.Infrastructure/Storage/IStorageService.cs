namespace ImperaOps.Infrastructure.Storage;

public interface IStorageService
{
    Task UploadAsync(string key, Stream stream, string contentType, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task EnsureBucketExistsAsync(CancellationToken ct = default);
}
