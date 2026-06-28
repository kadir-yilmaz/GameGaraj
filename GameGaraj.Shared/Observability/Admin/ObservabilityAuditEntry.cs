namespace GameGaraj.Shared.Observability.Admin
{
    /// <summary>
    /// Represents a single audit entry for observability configuration changes.
    /// Tracks who changed what, when, and why.
    /// </summary>
    public sealed record ObservabilityAuditEntry
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public required string ChangedBy { get; init; }
        public required string ServiceName { get; init; }
        public required string ChangeType { get; init; }    // "LogLevel" | "TraceSampling"
        public required string OldValue { get; init; }
        public required string NewValue { get; init; }
        public string? Reason { get; init; }
    }

    /// <summary>
    /// In-memory audit log for observability changes.
    /// In production, this should be backed by a persistent store.
    /// </summary>
    public sealed class ObservabilityAuditLog
    {
        private readonly List<ObservabilityAuditEntry> _entries = new();
        private readonly object _lock = new();
        private const int MaxEntries = 1000;

        public void Add(ObservabilityAuditEntry entry)
        {
            lock (_lock)
            {
                _entries.Add(entry);
                // Keep only the last N entries to prevent unbounded growth
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
                }
            }
        }

        public IReadOnlyList<ObservabilityAuditEntry> GetEntries(int limit = 50)
        {
            lock (_lock)
            {
                return _entries
                    .OrderByDescending(e => e.Timestamp)
                    .Take(limit)
                    .ToList()
                    .AsReadOnly();
            }
        }
    }
}
