namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Central constants for the observability platform.
    /// Service names follow OpenTelemetry semantic conventions.
    /// </summary>
    public static class ObservabilityConstants
    {
        // ── Service Names ──
        public const string GatewayService = "GameGaraj.Gateway";
        public const string CatalogService = "GameGaraj.Catalog";
        public const string BasketService = "GameGaraj.Basket";
        public const string OrderService = "GameGaraj.Order";
        public const string PaymentService = "GameGaraj.Payment";
        public const string InvoiceService = "GameGaraj.Invoice";
        public const string ReviewService = "GameGaraj.Review";
        public const string CampaignService = "GameGaraj.Campaign";
        public const string PhotoStockService = "GameGaraj.PhotoStock";

        // ── Metric Name Prefixes ──
        public const string MetricPrefix = "gamegaraj";
    }
}
