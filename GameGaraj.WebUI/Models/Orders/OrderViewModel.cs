namespace GameGaraj.WebUI.Models.Orders
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public string BuyerId { get; set; } = string.Empty;
        public AddressViewModel Address { get; set; } = new();
        public List<OrderItemViewModel> OrderItems { get; set; } = new();
        public int Status { get; set; } // 0=Pending, 1=Completed, 2=Failed
        
        public string StatusText => Status switch
        {
            0 => "Pending",
            1 => "Completed",
            2 => "Failed",
            _ => "Unknown"
        };
    }

    public class OrderItemViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PictureUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class AddressViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
    }

    public class OrderCreatedViewModel
    {
        public int OrderId { get; set; }
        public bool IsSuccessful { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
