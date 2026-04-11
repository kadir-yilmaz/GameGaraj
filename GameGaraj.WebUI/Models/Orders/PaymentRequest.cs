namespace GameGaraj.WebUI.Models.Orders
{
    public class PaymentRequest
    {
        public int OrderId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string ExpireMonth { get; set; } = string.Empty;
        public string ExpireYear { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        
        // Müşteri Bilgileri
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerSurname { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerIdentityNumber { get; set; } = "11111111111"; // Test için
        public string CustomerIp { get; set; } = "85.34.78.112"; // Test için
        
        // Adres Bilgileri
        public string AddressDetail { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = "Turkey";
        public string ZipCode { get; set; } = string.Empty;
        
        // Sepet Ürünleri
        public List<PaymentItem> Items { get; set; } = new();
    }

    public class PaymentItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PaymentId { get; set; }
    }
}
