using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Models.Auth
{
    public class ResetPasswordOtpViewModel
    {
        [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen 6 haneli doğrulama kodunu giriniz.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Doğrulama kodu 6 haneli olmalıdır.")]
        [Display(Name = "Doğrulama Kodu")]
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen yeni şifrenizi giriniz.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifreniz en az 6 karakter olmalıdır.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen şifrenizi tekrar giriniz.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Girdiğiniz şifreler birbiriyle eşleşmiyor.")]
        [Display(Name = "Yeni Şifre Tekrar")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
