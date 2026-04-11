namespace GameGaraj.WebUI.Models.Addresses
{
    public class UserAddressViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AddressType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
        
        public DateTime CreatedDate { get; set; }
    }

    public enum AddressType
    {
        Delivery = 1,
        Invoice = 2
    }

    public class CreateUserAddressInput
    {
        public AddressType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        
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

    public class UpdateUserAddressInput : CreateUserAddressInput
    {
        public int Id { get; set; }
    }

    public class AddressListViewModel
    {
        public List<UserAddressViewModel> DeliveryAddresses { get; set; } = new();
        public List<UserAddressViewModel> InvoiceAddresses { get; set; } = new();
        public AddressType ActiveTab { get; set; } = AddressType.Delivery;
    }
}
