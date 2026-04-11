namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Ödeme başarılı olduğunda fatura emaili göndermek için yayınlanan event.
    /// Payment.API tarafından publish edilir, Invoice.API tarafından consume edilir.
    /// </summary>
    public class InvoiceRequested
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public List<InvoiceItemMessage> Items { get; set; } = new();
    }

    public class InvoiceItemMessage
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
