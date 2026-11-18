using System.ComponentModel.DataAnnotations;
using GameGaraj.WebUI.Validation;

namespace GameGaraj.WebUI.Models.Orders
{
    public class CheckoutInfoInput
    {
        // Müşteri Bilgileri
        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        public string CustomerSurname { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon numarası zorunludur.")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
        public string CustomerPhone { get; set; } = string.Empty;

        // Adres Bilgileri
        [Required(ErrorMessage = "İl seçimi zorunludur.")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "İlçe seçimi zorunludur.")]
        public string District { get; set; } = string.Empty;

        public string Street { get; set; } = string.Empty;

        [Required(ErrorMessage = "Açık adres zorunludur.")]
        public string Line { get; set; } = string.Empty;

        public string ZipCode { get; set; } = string.Empty;

        // Ödeme Bilgileri
        [Required(ErrorMessage = "Kart üzerindeki isim zorunludur.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Kart üzerindeki isim en az 3 karakter olmalıdır.")]
        public string CardName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kart numarası zorunludur.")]
        [LuhnCardNumber]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Son kullanma tarihi zorunludur.")]
        [CardExpiration]
        public string Expiration { get; set; } = string.Empty; // MM/YY

        [Required(ErrorMessage = "CVV kodu zorunludur.")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV 3 veya 4 haneli rakam olmalıdır.")]
        public string CVV { get; set; } = string.Empty;

        // Adres Kaydetme Opsiyonları
        public bool SaveAddress { get; set; }
        public string AddressTitle { get; set; } = string.Empty;
    }
}
