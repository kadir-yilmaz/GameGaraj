namespace GameGaraj.WebUI.Models.Orders
{
    public class CheckoutInfoInput
    {
        // Müşteri Bilgileri
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerSurname { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        // Adres Bilgileri
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;

        // Ödeme Bilgileri
        public string CardName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public string Expiration { get; set; } = string.Empty; // MM/YY
        public string CVV { get; set; } = string.Empty;

        // Adres Kaydetme Opsiyonları
        public bool SaveAddress { get; set; }
        public string AddressTitle { get; set; } = string.Empty;
    }
}
