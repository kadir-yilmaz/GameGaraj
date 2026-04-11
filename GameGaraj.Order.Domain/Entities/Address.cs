using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Sipariş adresi (snapshot - sipariş anında UserAddress'ten kopyalanır)
    /// Adres değişse bile sipariş etkilenmez
    /// </summary>
    public class Address
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Adres tipi (Teslimat veya Fatura)
        /// </summary>
        public AddressType Type { get; set; } = AddressType.Delivery;
        
        // Kişi bilgileri
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // Adres detayları
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
    }
}
