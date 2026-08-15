using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Models.Auth
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;
    }
}
