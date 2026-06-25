namespace GameGaraj.Review.API.Services;

public interface IOrderOwnershipClient
{
    Task<OrderOwnershipResult> GetOwnershipAsync(string userId, string productId, CancellationToken cancellationToken);
}

public class OrderOwnershipResult
{
    public bool Owns { get; set; }
    public DateTime? PurchaseDate { get; set; }
}
