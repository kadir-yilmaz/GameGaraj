namespace GameGaraj.Shared.Chaos
{
    public class ChaosRule
    {
        public string ServiceName { get; set; } = string.Empty; // catalog, order, payment, invoice, campaign
        public bool Enabled { get; set; } = false;
        public bool AlwaysFail { get; set; } = false;
        public int FailUntilAttempt { get; set; } = 0; // e.g. 3 -> attempts 1 and 2 fail (503), 3 succeeds
        public int LatencyMs { get; set; } = 0; // Synthetic latency
        public bool SimulateStockUnavailable { get; set; } = false; // For Catalog API: declare all products out of stock
        public bool SimulatePaymentDeclined { get; set; } = false; // For Payment API: return 400 Insufficient Funds
        public bool BlockPipeline { get; set; } = false; // Completely drops / fails
        public int CurrentAttempts { get; set; } = 0; // Tracked attempt count
        public DateTime? LastTriggeredAt { get; set; }
        public string? Note { get; set; }
    }
}
