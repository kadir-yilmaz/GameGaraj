namespace GameGaraj.Shared.Dtos
{
    public class StockValidationRequest
    {
        public List<StockValidationItem> Items { get; set; } = new();
    }

    public class StockValidationItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
    }

    public class StockValidationResponse
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<StockValidationItemStatus> ItemStatuses { get; set; } = new();
    }

    public class StockValidationItemStatus
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int AvailableStock { get; set; }
        public bool IsAvailable { get; set; }
    }
}
