using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface IProductIndexService
    {
        Task EnsureIndexAsync(bool recreate = false);
        Task IndexAsync(Product product);
        Task DeleteAsync(string productId);
        Task<ReindexResultDto> ReindexAllAsync();
        Task<SearchIndexStatusDto> GetStatusAsync();
        Task<SearchIndexDocumentPageDto> GetDocumentPreviewsAsync(int page = 1, int pageSize = 100);
    }
}
