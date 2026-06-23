using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    public class CarouselImageService : ICarouselImageService
    {
        private readonly string _connectionString;

        public CarouselImageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlServer")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<CarouselImage>> GetAllAsync()
        {
            const string query = "SELECT Id, ImageUrl, DisplayOrder, CreatedTime FROM CarouselImages ORDER BY DisplayOrder ASC, CreatedTime DESC";
            using var connection = CreateConnection();
            var images = await connection.QueryAsync<CarouselImage>(query);
            return images.ToList();
        }

        public async Task<CarouselImage?> GetByIdAsync(int id)
        {
            const string query = "SELECT Id, ImageUrl, DisplayOrder, CreatedTime FROM CarouselImages WHERE Id = @Id";
            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<CarouselImage>(query, new { Id = id });
        }

        public async Task<bool> SaveAsync(CarouselImage image)
        {
            const string query = @"INSERT INTO CarouselImages (ImageUrl, DisplayOrder) 
                                   VALUES (@ImageUrl, @DisplayOrder)";
            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, image);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM CarouselImages WHERE Id = @Id";
            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}
