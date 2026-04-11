using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Application.Services.Abstract
{
    public interface IUserAddressService
    {
        Task<List<UserAddressDto>> GetUserAddressesAsync(string userId, AddressType? type = null);
        Task<UserAddressDto?> GetByIdAsync(int id, string userId);
        Task<UserAddressDto?> GetDefaultAddressAsync(string userId, AddressType type);
        Task<UserAddressDto> CreateAsync(string userId, CreateUserAddressDto dto);
        Task<bool> UpdateAsync(string userId, UpdateUserAddressDto dto);
        Task<bool> DeleteAsync(int id, string userId);
        Task<bool> SetAsDefaultAsync(int id, string userId, AddressType type);
    }
}
