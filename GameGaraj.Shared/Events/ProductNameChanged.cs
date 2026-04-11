namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Ürün adı değiştiğinde Catalog API tarafından publish edilir.
    /// Order Service tarafından consume edilerek OrderItem.ProductName güncellenir.
    /// Eventual Consistency için kullanılır.
    /// </summary>
    public class ProductNameChanged
    {
        public string ProductId { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
    }
}
