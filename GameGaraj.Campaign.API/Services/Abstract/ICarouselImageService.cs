using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    public interface ICarouselImageService
    {
        Task<List<CarouselImage>> GetAllAsync();
        Task<CarouselImage?> GetByIdAsync(int id);
        Task<bool> SaveAsync(CarouselImage image);
        Task<bool> DeleteAsync(int id);
    }
}
