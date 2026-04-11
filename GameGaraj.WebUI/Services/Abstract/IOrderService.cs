using GameGaraj.WebUI.Models.Addresses;
using GameGaraj.WebUI.Models.Orders;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IOrderService
    {
        Task<OrderCreatedViewModel> CreateOrder(CheckoutInfoInput checkoutInfoInput);
        Task<List<OrderViewModel>> GetOrders();
        
        // Admin Methods
        Task<List<OrderViewModel>> GetAllOrdersAsync();
        Task<bool> UpdateOrderStatusAsync(int orderId, int status);
        Task<bool> ShipOrderAsync(int orderId);
        
        // Address Management
        Task<List<UserAddressViewModel>> GetUserAddressesAsync(AddressType? type = null);
        Task<UserAddressViewModel?> GetAddressByIdAsync(int id);
        Task<UserAddressViewModel?> GetDefaultAddressAsync(AddressType type);
        Task<UserAddressViewModel?> CreateAddressAsync(CreateUserAddressInput input);
        Task<bool> UpdateAddressAsync(UpdateUserAddressInput input);
        Task<bool> DeleteAddressAsync(int id);
        Task<bool> SetAsDefaultAsync(int id, AddressType type);
    }
}
