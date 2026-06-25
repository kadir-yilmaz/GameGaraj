namespace GameGaraj.PhotoStock.API.Models;

public sealed record StorageHealthResult(
    bool Healthy,
    string Provider,
    string? BucketName = null,
    string? Message = null);
