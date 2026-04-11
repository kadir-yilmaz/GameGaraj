using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Application.Dtos
{
    /// <summary>
    /// Kullanıcı adresi DTO
    /// </summary>
    public class UserAddressDto
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
    
    /// <summary>
    /// Adres oluşturma/güncelleme DTO
    /// </summary>
    public class CreateUserAddressDto
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
    
    /// <summary>
    /// Adres güncelleme DTO
    /// </summary>
    public class UpdateUserAddressDto : CreateUserAddressDto
    {
        public int Id { get; set; }
    }
}
