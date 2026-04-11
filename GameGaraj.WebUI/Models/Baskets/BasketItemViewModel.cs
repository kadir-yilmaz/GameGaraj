namespace GameGaraj.WebUI.Models.Baskets
{
    public class BasketItemViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
