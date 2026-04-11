using AutoMapper;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Domain.Entities;

namespace GameGaraj.Order.Application.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Domain.Entities.Order, OrderDto>()
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.DeliveryAddress))
                .ReverseMap();
            CreateMap<OrderItem, OrderItemDto>().ReverseMap();
            CreateMap<Address, AddressDto>().ReverseMap();
        }
    }
}
