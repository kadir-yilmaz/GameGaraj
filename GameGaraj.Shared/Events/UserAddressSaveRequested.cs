namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Checkout sayfasında "Adresi Kaydet" seçeneği işaretlendiğinde asenkron olarak fırlatılan olay.
    /// WebUI tarafından publish edilir, Order.API tarafından consume edilir.
    /// </summary>
    public class UserAddressSaveRequested
    {
        public string UserId { get; set; } = string.Empty;
        public int Type { get; set; } // 1: Delivery, 2: Invoice
        public string Title { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
    }
}
