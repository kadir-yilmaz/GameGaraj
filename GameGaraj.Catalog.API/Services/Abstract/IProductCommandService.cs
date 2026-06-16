using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface IProductCommandService
    {
        Task<ProductDto> CreateAsync(ProductCreateDto dto);
        Task<bool> UpdateAsync(ProductUpdateDto dto);
        Task<bool> DeleteAsync(string id);
    }
}
