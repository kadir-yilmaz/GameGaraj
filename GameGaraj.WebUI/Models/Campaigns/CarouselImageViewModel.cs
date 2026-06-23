using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Models.Campaigns
{
    public class CarouselImageViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Resim URL'si veya dosya yükleme zorunludur.")]
        [Display(Name = "Görsel URL")]
        public string ImageUrl { get; set; } = string.Empty;

        [Display(Name = "Görüntüleme Sırası")]
        public int DisplayOrder { get; set; }

        public DateTime CreatedTime { get; set; }
    }
}
