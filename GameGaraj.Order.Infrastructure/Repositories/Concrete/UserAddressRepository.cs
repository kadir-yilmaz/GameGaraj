using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Order.Infrastructure.Repositories.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Order.Infrastructure.Repositories.Concrete
{
    public class UserAddressRepository : IUserAddressRepository
    {
        private readonly OrderDbContext _context;

        public UserAddressRepository(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserAddress>> GetUserAddressesAsync(string userId, AddressType? type = null)
        {
            var query = _context.UserAddresses.Where(a => a.UserId == userId);
            
            if (type.HasValue)
            {
                query = query.Where(a => a.Type == type.Value);
            }
            
            return await query
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<UserAddress?> GetByIdAsync(int id, string userId)
        {
            return await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        }

        public async Task<UserAddress?> GetDefaultAddressAsync(string userId, AddressType type)
        {
            return await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Type == type && a.IsDefault);
        }

        public async Task<int> GetAddressCountAsync(string userId, AddressType type)
        {
            return await _context.UserAddresses
                .CountAsync(a => a.UserId == userId && a.Type == type);
        }

        public async Task<UserAddress> CreateAsync(UserAddress address)
        {
            _context.UserAddresses.Add(address);
            await _context.SaveChangesAsync();
            return address;
        }

        public async Task<bool> UpdateAsync(UserAddress address)
        {
            _context.UserAddresses.Update(address);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var address = await GetByIdAsync(id, userId);
            if (address == null)
                return false;

            _context.UserAddresses.Remove(address);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> SetAsDefaultAsync(int id, string userId, AddressType type)
        {
            // Önce aynı tipteki tüm adreslerin IsDefault'unu false yap
            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == userId && a.Type == type)
                .ToListAsync();

            foreach (var addr in addresses)
            {
                addr.IsDefault = addr.Id == id;
            }

            return await _context.SaveChangesAsync() > 0;
        }
    }
}
