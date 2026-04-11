using AutoMapper;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Domain.Entities;

namespace GameGaraj.Order.Application.Mapping
{
    public class OrderMappingProfile : Profile
    {
        public OrderMappingProfile()
        {
            // UserAddress mappings
            CreateMap<UserAddress, UserAddressDto>();
            CreateMap<CreateUserAddressDto, UserAddress>();
            CreateMap<UpdateUserAddressDto, UserAddress>();
            
            // Order mappings (existing - if any)
            // CreateMap<Order, OrderDto>();
        }
    }
}
