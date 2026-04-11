namespace GameGaraj.WebUI.Models.Baskets
{
    public class BasketViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public List<BasketItemViewModel> Items { get; set; } = new();
        public decimal TotalPrice => Items.Sum(x => x.Price * x.Quantity);
    }
}
