namespace GameGaraj.Basket.API.Data;

public class BasketItem
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public string? PictureUrl { get; set; }
    public int Quantity { get; set; }
}
