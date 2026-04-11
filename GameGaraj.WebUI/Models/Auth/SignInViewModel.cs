using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Models.Auth
{
    public class SignInViewModel
    {
        [Required(ErrorMessage = "Email adresi gereklidir.")]
        [Display(Name = "Email Adresiniz")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Şifre gereklidir.")]
        [Display(Name = "Şifreniz")]
        public string Password { get; set; } = null!;

        [Display(Name = "Beni Hatırla")]
        public bool IsRemember { get; set; }
    }
}
