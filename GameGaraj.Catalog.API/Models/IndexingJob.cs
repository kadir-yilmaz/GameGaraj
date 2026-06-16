namespace GameGaraj.Catalog.API.Models
{
    public class IndexingJob
    {
        public string Id { get; set; } = string.Empty;
        public string EntityType { get; set; } = "Product";
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Status { get; set; } = IndexingJobStatus.Pending;
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public static class IndexingJobOperation
    {
        public const string Upsert = "Upsert";
        public const string Delete = "Delete";
    }

    public static class IndexingJobStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }
}
