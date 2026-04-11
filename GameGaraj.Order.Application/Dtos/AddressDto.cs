namespace GameGaraj.Order.Application.Dtos
{
    public class AddressDto
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
}
