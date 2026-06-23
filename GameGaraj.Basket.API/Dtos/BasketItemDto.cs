using System.ComponentModel.DataAnnotations;

namespace GameGaraj.Basket.API.Dtos;

public record BasketItemDto(
    [Required] string Id, 
    [Required] string Name, 
    [Required] decimal Price, 
    string? PictureUrl, 
    [Range(1, int.MaxValue)] int Quantity,
    string? CategoryId = null,
    string? Brand = null,
    string? ProductSlug = null);
