namespace GameGaraj.WebUI.Settings
{
    public class ServiceApiSettings
    {
        public string IdentityBaseUri { get; set; } = string.Empty;
        public string GatewayBaseUri { get; set; } = string.Empty;
        public string PhotoStockUri { get; set; } = string.Empty;
        
        // Direkt API URL'leri (Gateway olmadan)
        public string CatalogUri { get; set; } = string.Empty;
        public string BasketUri { get; set; } = string.Empty;
        public string DiscountUri { get; set; } = string.Empty;
        public string PaymentUri { get; set; } = string.Empty;
        public string OrderUri { get; set; } = string.Empty;

        public ServiceApi Catalog { get; set; } = new();
        public ServiceApi PhotoStock { get; set; } = new();
        public ServiceApi Basket { get; set; } = new();
        public ServiceApi Discount { get; set; } = new();
        public ServiceApi Payment { get; set; } = new();
        public ServiceApi Order { get; set; } = new();
    }
}
