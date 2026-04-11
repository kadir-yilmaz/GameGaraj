namespace GameGaraj.Shared.Events
{
    public class StockNotReserved
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
