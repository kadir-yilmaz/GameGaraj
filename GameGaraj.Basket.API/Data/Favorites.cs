namespace GameGaraj.Basket.API.Data;

public class Favorites
{
    public string UserId { get; set; } = default!;
    public List<string> ProductIds { get; set; } = new();
}
