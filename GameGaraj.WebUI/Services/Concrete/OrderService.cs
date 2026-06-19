using GameGaraj.WebUI.Models.Addresses;
using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Models.Common;
using GameGaraj.WebUI.Models.Orders;
using GameGaraj.WebUI.Services.Abstract;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class OrderService : IOrderService
    {
        private readonly HttpClient _httpClient;
        private readonly IBasketService _basketService;
        private readonly ILogger<OrderService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderService(
            HttpClient httpClient, 
            IBasketService basketService,
            ILogger<OrderService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _basketService = basketService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<OrderCreatedViewModel> CreateOrder(CheckoutInfoInput checkoutInfoInput, OrderPricingSnapshot pricingSnapshot)
        {
            try
            {
                // Sepeti al
                var basket = await _basketService.GetBasketAsync();
                
                if (basket == null || !basket.Items.Any())
                {
                    return new OrderCreatedViewModel 
                    { 
                        IsSuccessful = false, 
                        Error = "Sepetiniz boş" 
                    };
                }

                // Order oluştur
                var orderCreateInput = new
                {
                    BuyerId = basket.UserId,
                    OriginalTotalAmount = pricingSnapshot.OriginalTotalAmount,
                    CampaignDiscountAmount = pricingSnapshot.CampaignDiscountAmount,
                    CouponDiscountAmount = pricingSnapshot.CouponDiscountAmount,
                    ShippingFee = pricingSnapshot.ShippingFee,
                    TotalPaidAmount = pricingSnapshot.TotalPaidAmount,
                    CouponCode = pricingSnapshot.CouponCode,
                    AppliedCampaignName = pricingSnapshot.AppliedCampaignName,
                    Address = new
                    {
                        FirstName = checkoutInfoInput.CustomerName,
                        LastName = checkoutInfoInput.CustomerSurname,
                        PhoneNumber = checkoutInfoInput.CustomerPhone,
                        Email = checkoutInfoInput.CustomerEmail,
                        Province = checkoutInfoInput.Province,
                        District = checkoutInfoInput.District,
                        Neighborhood = checkoutInfoInput.Street,
                        PostalCode = checkoutInfoInput.ZipCode,
                        AddressDetail = checkoutInfoInput.Line
                    },
                    OrderItems = basket.Items.Select(x => new
                    {
                        ProductId = x.ProductId,
                        ProductName = x.ProductName,
                        Price = x.Price,
                        PictureUrl = x.ImageUrl,
                        Quantity = x.Quantity,
                        DiscountAmount = 0m // Not: Eğer kalem bazlı kampanya verisi varsa buraya eşlenmeli
                    }).ToList(),
                    OrderDiscounts = pricingSnapshot.OrderPricingLedgers.Select(d => new
                    {
                        Title = d.Title,
                        Amount = d.Amount
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(orderCreateInput);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"[OrderService] Creating order for user: {basket.UserId}");
                
                var response = await _httpClient.PostAsync("orders", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[OrderService] Order creation failed: {errorContent}");
                    return new OrderCreatedViewModel 
                    { 
                        IsSuccessful = false, 
                        Error = "Sipariş oluşturulamadı" 
                    };
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"[OrderService] Order created successfully. Raw response: {responseContent}");
                    
                    if (int.TryParse(responseContent, out int orderId))
                    {
                        // Sipariş başarılı, sepeti temizle
                        await _basketService.DeleteAsync();
                        
                        return new OrderCreatedViewModel 
                        { 
                            OrderId = orderId,
                            IsSuccessful = true 
                        };
                    }
                }

                return new OrderCreatedViewModel 
                { 
                    IsSuccessful = false, 
                    Error = "Sipariş yanıtı alınamadı" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error creating order");
                return new OrderCreatedViewModel 
                { 
                    IsSuccessful = false, 
                    Error = "Bir hata oluştu" 
                };
            }
        }

        public async Task<List<OrderViewModel>> GetOrders()
        {
            try
            {
                // UserId'yi HttpContext'ten al
                var userId = _httpContextAccessor.HttpContext?.User?.Claims
                    .FirstOrDefault(x => x.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("[OrderService] UserId not found in 'sub' claim, trying alternative claims");
                    
                    // Alternatif claim'leri dene
                    userId = _httpContextAccessor.HttpContext?.User?.Claims
                        .FirstOrDefault(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    }
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        _logger.LogError("[OrderService] UserId could not be found in any claim");
                        _logger.LogInformation($"[OrderService] Available claims: {string.Join(", ", _httpContextAccessor.HttpContext?.User?.Claims.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>())}");
                        return new List<OrderViewModel>();
                    }
                }

                _logger.LogInformation($"[OrderService] Fetching orders for userId: {userId}");

                var response = await _httpClient.GetAsync($"orders/{userId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"[OrderService] GetOrders failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"[OrderService] Error content: {errorContent}");
                    return new List<OrderViewModel>();
                }

                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation($"[OrderService] Response content length: {content.Length}");
                _logger.LogInformation($"[OrderService] Response content: {content}");

                var orders = JsonSerializer.Deserialize<List<OrderViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"[OrderService] Deserialized {orders?.Count ?? 0} orders");

                return orders ?? new List<OrderViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error fetching orders");
                return new List<OrderViewModel>();
            }
        }

        #region Address Management

        public async Task<List<UserAddressViewModel>> GetUserAddressesAsync(AddressType? type = null)
        {
            try
            {
                var url = type.HasValue ? $"useraddresses?type={type}" : "useraddresses";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    return new List<UserAddressViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                var addresses = JsonSerializer.Deserialize<List<UserAddressViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return addresses ?? new List<UserAddressViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error fetching addresses");
                return new List<UserAddressViewModel>();
            }
        }

        public async Task<UserAddressViewModel?> GetAddressByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"useraddresses/{id}");
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var address = JsonSerializer.Deserialize<UserAddressViewModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return address;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error fetching address {id}");
                return null;
            }
        }

        public async Task<UserAddressViewModel?> GetDefaultAddressAsync(AddressType type)
        {
            try
            {
                var response = await _httpClient.GetAsync($"useraddresses/default/{type}");
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var address = JsonSerializer.Deserialize<UserAddressViewModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return address;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error fetching default address for type {type}");
                return null;
            }
        }

        public async Task<UserAddressViewModel?> CreateAddressAsync(CreateUserAddressInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("useraddresses", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[OrderService] Address creation failed: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var address = JsonSerializer.Deserialize<UserAddressViewModel>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return address;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error creating address");
                return null;
            }
        }

        public async Task<bool> UpdateAddressAsync(UpdateUserAddressInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"useraddresses/{input.Id}", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error updating address {input.Id}");
                return false;
            }
        }

        public async Task<bool> DeleteAddressAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"useraddresses/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error deleting address {id}");
                return false;
            }
        }

        public async Task<bool> SetAsDefaultAsync(int id, AddressType type)
        {
            try
            {
                var response = await _httpClient.PostAsync($"useraddresses/{id}/set-default?type={type}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error setting default address {id}");
                return false;
            }
        }

        #endregion

        #region Admin Methods

        public async Task<List<OrderViewModel>> GetAllOrdersAsync()
        {
            try
            {
                _logger.LogInformation("[OrderService] Fetching all orders for admin");

                var response = await _httpClient.GetAsync("orders/all");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"[OrderService] GetAllOrders failed: {response.StatusCode}");
                    return new List<OrderViewModel>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<List<OrderViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"[OrderService] Retrieved {orders?.Count ?? 0} orders");

                return orders ?? new List<OrderViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error fetching all orders");
                return new List<OrderViewModel>();
            }
        }

        public async Task<PagedResultViewModel<OrderViewModel>> GetAdminOrdersPageAsync(string? query = null, int? status = null, DateTime? dateFrom = null, DateTime? dateTo = null, int page = 1, int pageSize = 12)
        {
            try
            {
                var queryParams = new List<string>
                {
                    $"page={page}",
                    $"pageSize={pageSize}"
                };

                if (!string.IsNullOrWhiteSpace(query))
                    queryParams.Add($"q={Uri.EscapeDataString(query)}");

                if (status.HasValue)
                    queryParams.Add($"status={status.Value}");

                if (dateFrom.HasValue)
                    queryParams.Add($"dateFrom={dateFrom.Value:yyyy-MM-dd}");

                if (dateTo.HasValue)
                    queryParams.Add($"dateTo={dateTo.Value:yyyy-MM-dd}");

                var response = await _httpClient.GetAsync($"orders/admin?{string.Join("&", queryParams)}");
                if (!response.IsSuccessStatusCode)
                    return new PagedResultViewModel<OrderViewModel> { Page = page, PageSize = pageSize };

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResultViewModel<OrderViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PagedResultViewModel<OrderViewModel> { Page = page, PageSize = pageSize };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrderService] Error fetching admin orders page");
                return new PagedResultViewModel<OrderViewModel> { Page = page, PageSize = pageSize };
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, int status)
        {
            try
            {
                _logger.LogInformation($"[OrderService] Updating order {orderId} status to {status}");

                var response = await _httpClient.PutAsync($"orders/{orderId}/status/{status}", null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[OrderService] Order {orderId} status updated successfully");
                    return true;
                }

                _logger.LogWarning($"[OrderService] Failed to update order {orderId} status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error updating order {orderId} status");
                return false;
            }
        }

        public async Task<bool> ShipOrderAsync(int orderId)
        {
            try
            {
                _logger.LogInformation($"[OrderService] Shipping order {orderId}");

                // Status 4 = Shipped
                var response = await _httpClient.PutAsync($"orders/{orderId}/status/4", null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[OrderService] Order {orderId} shipped successfully");
                    return true;
                }

                _logger.LogWarning($"[OrderService] Failed to ship order {orderId}: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[OrderService] Error shipping order {orderId}");
                return false;
            }
        }

        #endregion
    }
}
