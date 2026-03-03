using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FreightVis.Infrastructure.Storage;

public sealed class S3StorageService : IStorageService, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IConfiguration config, ILogger<S3StorageService> logger)
    {
        _logger = logger;

        var serviceUrl = config["Storage:ServiceUrl"] ?? throw new InvalidOperationException("Missing Storage:ServiceUrl");
        var accessKey  = config["Storage:AccessKey"]  ?? throw new InvalidOperationException("Missing Storage:AccessKey");
        var secretKey  = config["Storage:SecretKey"]  ?? throw new InvalidOperationException("Missing Storage:SecretKey");
        var region     = config["Storage:Region"]     ?? "us-east-1";
        _bucketName    = config["Storage:BucketName"] ?? throw new InvalidOperationException("Missing Storage:BucketName");

        _client = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
        {
            ServiceURL           = serviceUrl,
            ForcePathStyle       = true,
            AuthenticationRegion = region,
        });
    }

    public async Task UploadAsync(string key, Stream stream, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName      = _bucketName,
            Key             = key,
            InputStream     = stream,
            ContentType     = contentType,
            AutoCloseStream = false,
        };
        await _client.PutObjectAsync(request, ct);
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key        = key,
            Expires    = DateTime.UtcNow.Add(expiry),
            Protocol   = Protocol.HTTP,
        };
        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucketName, key, ct);
    }

    public async Task EnsureBucketExistsAsync(CancellationToken ct = default)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucketName);
        if (!exists)
        {
            await _client.PutBucketAsync(_bucketName, ct);
            _logger.LogInformation("Created storage bucket '{Bucket}'.", _bucketName);
        }
        else
        {
            _logger.LogInformation("Storage bucket '{Bucket}' is ready.", _bucketName);
        }
    }

    public void Dispose() => _client.Dispose();
}
