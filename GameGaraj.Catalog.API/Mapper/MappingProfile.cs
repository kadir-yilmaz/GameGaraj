using AutoMapper;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;

namespace GameGaraj.Catalog.API.Mapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Category
            CreateMap<Category, CategoryDto>();

            // CategoryAttribute
            CreateMap<CategoryAttribute, CategoryAttributeDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

            // Product
            CreateMap<Product, ProductDto>();
        }
    }
}
