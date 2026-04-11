using AutoMapper;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Services.Abstract;
using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure.Repositories.Abstract;

namespace GameGaraj.Order.Application.Services.Concrete
{
    public class UserAddressService : IUserAddressService
    {
        private readonly IUserAddressRepository _repository;
        private readonly IMapper _mapper;
        private const int MaxAddressesPerType = 3;

        public UserAddressService(IUserAddressRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<List<UserAddressDto>> GetUserAddressesAsync(string userId, AddressType? type = null)
        {
            var addresses = await _repository.GetUserAddressesAsync(userId, type);
            return _mapper.Map<List<UserAddressDto>>(addresses);
        }

        public async Task<UserAddressDto?> GetByIdAsync(int id, string userId)
        {
            var address = await _repository.GetByIdAsync(id, userId);
            return address == null ? null : _mapper.Map<UserAddressDto>(address);
        }

        public async Task<UserAddressDto?> GetDefaultAddressAsync(string userId, AddressType type)
        {
            var address = await _repository.GetDefaultAddressAsync(userId, type);
            return address == null ? null : _mapper.Map<UserAddressDto>(address);
        }

        public async Task<UserAddressDto> CreateAsync(string userId, CreateUserAddressDto dto)
        {
            // Adres sayısı kontrolü (max 3 per type)
            var count = await _repository.GetAddressCountAsync(userId, dto.Type);
            if (count >= MaxAddressesPerType)
            {
                throw new InvalidOperationException($"Maksimum {MaxAddressesPerType} adet {dto.Type} adresi ekleyebilirsiniz.");
            }

            var address = _mapper.Map<UserAddress>(dto);
            address.UserId = userId;

            // Eğer bu ilk adres ise veya IsDefault true ise, varsayılan yap
            if (count == 0 || dto.IsDefault)
            {
                address.IsDefault = true;
                // Diğer adreslerin IsDefault'unu false yap
                if (dto.IsDefault)
                {
                    await _repository.SetAsDefaultAsync(0, userId, dto.Type); // 0 = tümünü false yap
                }
            }

            var created = await _repository.CreateAsync(address);
            return _mapper.Map<UserAddressDto>(created);
        }

        public async Task<bool> UpdateAsync(string userId, UpdateUserAddressDto dto)
        {
            var existing = await _repository.GetByIdAsync(dto.Id, userId);
            if (existing == null)
                return false;

            // Güvenlik kontrolü - sadece kendi adresini güncelleyebilir
            if (existing.UserId != userId)
                throw new UnauthorizedAccessException("Bu adresi güncelleme yetkiniz yok.");

            _mapper.Map(dto, existing);

            // Eğer IsDefault true yapılıyorsa, diğerlerini false yap
            if (dto.IsDefault && !existing.IsDefault)
            {
                await _repository.SetAsDefaultAsync(dto.Id, userId, dto.Type);
            }

            return await _repository.UpdateAsync(existing);
        }

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var address = await _repository.GetByIdAsync(id, userId);
            if (address == null)
                return false;

            // Güvenlik kontrolü
            if (address.UserId != userId)
                throw new UnauthorizedAccessException("Bu adresi silme yetkiniz yok.");

            var wasDefault = address.IsDefault;
            var type = address.Type;

            var result = await _repository.DeleteAsync(id, userId);

            // Eğer silinen adres varsayılandıysa, başka bir adresi varsayılan yap
            if (result && wasDefault)
            {
                var remaining = await _repository.GetUserAddressesAsync(userId, type);
                if (remaining.Any())
                {
                    await _repository.SetAsDefaultAsync(remaining.First().Id, userId, type);
                }
            }

            return result;
        }

        public async Task<bool> SetAsDefaultAsync(int id, string userId, AddressType type)
        {
            var address = await _repository.GetByIdAsync(id, userId);
            if (address == null)
                return false;

            // Güvenlik kontrolü
            if (address.UserId != userId)
                throw new UnauthorizedAccessException("Bu adresi varsayılan yapma yetkiniz yok.");

            return await _repository.SetAsDefaultAsync(id, userId, type);
        }
    }
}
