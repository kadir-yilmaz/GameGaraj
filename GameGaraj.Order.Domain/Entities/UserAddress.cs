using GameGaraj.Order.Domain.Common;
using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Kullanıcının kayıtlı adresleri (yeniden kullanılabilir)
    /// Her kullanıcı 3'er adet teslimat ve fatura adresi kaydedebilir
    /// </summary>
    public class UserAddress : BaseEntity
    {
        /// <summary>
        /// Kullanıcı ID (Identity'den)
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Adres tipi (Teslimat veya Fatura)
        /// </summary>
        public AddressType Type { get; set; } = AddressType.Delivery;
        
        /// <summary>
        /// Adres başlığı (Evim, İşim, Annem, vs.)
        /// </summary>
        public string Title { get; set; } = "Evim";
        
        /// <summary>
        /// Varsayılan adres mi? (Her tip için bir tane varsayılan olabilir)
        /// </summary>
        public bool IsDefault { get; set; }
        
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
