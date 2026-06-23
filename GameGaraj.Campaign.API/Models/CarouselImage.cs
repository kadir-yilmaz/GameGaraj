namespace GameGaraj.Campaign.API.Models
{
    public class CarouselImage
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
}
