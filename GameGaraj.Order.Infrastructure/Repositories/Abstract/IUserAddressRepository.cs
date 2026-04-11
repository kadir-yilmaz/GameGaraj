using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Infrastructure.Repositories.Abstract
{
    public interface IUserAddressRepository
    {
        Task<List<UserAddress>> GetUserAddressesAsync(string userId, AddressType? type = null);
        Task<UserAddress?> GetByIdAsync(int id, string userId);
        Task<UserAddress?> GetDefaultAddressAsync(string userId, AddressType type);
        Task<int> GetAddressCountAsync(string userId, AddressType type);
        Task<UserAddress> CreateAsync(UserAddress address);
        Task<bool> UpdateAsync(UserAddress address);
        Task<bool> DeleteAsync(int id, string userId);
        Task<bool> SetAsDefaultAsync(int id, string userId, AddressType type);
    }
}
