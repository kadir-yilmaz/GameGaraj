using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers;

[Authorize]
public class ReviewsController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly IOrderService _orderService;

    public ReviewsController(IReviewService reviewService, IOrderService orderService)
    {
        _reviewService = reviewService;
        _orderService = orderService;
    }

    public async Task<IActionResult> Index()
    {
        var reviews = await _reviewService.GetMyReviewsAsync();
        var reviewedProductIds = reviews
            .Select(review => review.ProductId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orders = await _orderService.GetOrders();
        var reviewableItems = orders
            .Where(order => order.Status == 5)
            .SelectMany(order => order.OrderItems.Select(item => new GameGaraj.WebUI.Models.Reviews.ReviewableProductViewModel
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductImageUrl = item.PictureUrl,
                OrderId = order.Id,
                OrderDate = order.CreatedDate
            }))
            .GroupBy(item => item.ProductId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.OrderDate).First())
            .Where(item => !reviewedProductIds.Contains(item.ProductId))
            .OrderByDescending(item => item.OrderDate)
            .ToList();

        ViewBag.ReviewableItems = reviewableItems;
        return View(reviews);
    }
}
