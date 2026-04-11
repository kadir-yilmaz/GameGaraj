namespace GameGaraj.Invoice.API.Models
{
    public class InvoiceData
    {
        public int OrderId { get; set; }
        public string InvoiceNumber => $"INV-{OrderId:D6}";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public List<InvoiceItem> Items { get; set; } = new();
    }

    public class InvoiceItem
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
