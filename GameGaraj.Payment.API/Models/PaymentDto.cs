namespace GameGaraj.Payment.API.Models
{
    /// <summary>
    /// Ödeme isteği modeli - iyzico entegrasyonu için
    /// </summary>
    public class PaymentDto
    {
        // Kart Bilgileri
        public string CardName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string ExpireMonth { get; set; } = string.Empty;
        public string ExpireYear { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;

        // Sipariş Bilgileri
        public decimal TotalPrice { get; set; }
        public int OrderId { get; set; }

        // Buyer (Alıcı) Bilgileri - iyzico için zorunlu
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerSurname { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerIdentityNumber { get; set; } = "11111111111"; // TC Kimlik - varsayılan
        public string CustomerIp { get; set; } = "127.0.0.1";

        // Adres Bilgileri - iyzico için zorunlu
        public string AddressDetail { get; set; } = string.Empty;
        public string City { get; set; } = "Istanbul";
        public string Country { get; set; } = "Turkey";
        public string ZipCode { get; set; } = "34000";

        // Ürünler
        public List<PaymentItemDto> Items { get; set; } = new();
    }

    public class PaymentItemDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
