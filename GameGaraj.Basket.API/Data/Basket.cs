namespace GameGaraj.Basket.API.Data;

public class Basket
{
    public string UserId { get; set; } = default!;
    public List<BasketItem> Items { get; set; } = new();
    public decimal TotalPrice => Items.Sum(x => x.Price * x.Quantity);
}
