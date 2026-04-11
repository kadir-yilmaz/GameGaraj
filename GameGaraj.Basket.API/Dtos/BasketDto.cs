using System.ComponentModel.DataAnnotations;

namespace GameGaraj.Basket.API.Dtos;

public record BasketDto(
    [Required] string UserId, 
    List<BasketItemDto> Items, 
    decimal TotalPrice);
