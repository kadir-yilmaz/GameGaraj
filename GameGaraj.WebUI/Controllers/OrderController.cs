using GameGaraj.Shared.Events;
using GameGaraj.WebUI.Models.Orders;
using GameGaraj.WebUI.Services.Abstract;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GameGaraj.WebUI.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IBasketService _basketService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IBasketService basketService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderController> logger)
        {
            _basketService = basketService;
            _orderService = orderService;
            _paymentService = paymentService;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<IActionResult> Checkout()
        {
            var basket = await _basketService.GetBasketAsync();

            if (basket == null || !basket.Items.Any())
            {
                TempData["Error"] = "Sepetiniz boş";
                return RedirectToAction("Index", "Basket");
            }

            // Kayıtlı adresleri getir
            var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
            var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

            ViewBag.Basket = basket;
            ViewBag.DeliveryAddresses = deliveryAddresses;
            ViewBag.InvoiceAddresses = invoiceAddresses;

            // Giriş yapmış kullanıcı bilgilerini ön tanımlı olarak getir
            var model = new CheckoutInfoInput();
            
            if (User.Identity.IsAuthenticated)
            {
                model.CustomerName = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value 
                                     ?? User.Claims.FirstOrDefault(x => x.Type == "name")?.Value ?? "";
                
                model.CustomerSurname = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value ?? "";
                
                model.CustomerEmail = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value 
                                      ?? User.Claims.FirstOrDefault(x => x.Type == "email")?.Value ?? "";
                
                model.CustomerPhone = User.Claims.FirstOrDefault(x => x.Type == "phone")?.Value 
                                      ?? User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.MobilePhone)?.Value ?? "";
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutInfoInput checkoutInfoInput)
        {
            _logger.LogInformation("[OrderController] ========== CHECKOUT POST STARTED ==========");
            _logger.LogInformation($"[OrderController] ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[OrderController] ModelState is invalid:");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"  - {error.ErrorMessage}");
                }

                var basket = await _basketService.GetBasketAsync();
                var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
                var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

                ViewBag.Basket = basket;
                ViewBag.DeliveryAddresses = deliveryAddresses;
                ViewBag.InvoiceAddresses = invoiceAddresses;

                return View(checkoutInfoInput);
            }

            _logger.LogInformation("[OrderController] Processing checkout");
            _logger.LogInformation($"[OrderController] Customer: {checkoutInfoInput.CustomerName} {checkoutInfoInput.CustomerSurname}");
            _logger.LogInformation($"[OrderController] Address: {checkoutInfoInput.Province}/{checkoutInfoInput.District}");
            _logger.LogInformation($"[OrderController] Card: {checkoutInfoInput.CardName}");

            // Sepeti session'a kaydet (Payment'ta kullanılacak)
            var basket2 = await _basketService.GetBasketAsync();

            _logger.LogInformation($"[OrderController] Basket before session:");
            _logger.LogInformation($"  - UserId: {basket2?.UserId}");
            _logger.LogInformation($"  - Items Count: {basket2?.Items?.Count ?? 0}");
            if (basket2?.Items != null)
            {
                foreach (var item in basket2.Items)
                {
                    _logger.LogInformation($"    * ProductId: {item.ProductId}, ProductName: '{item.ProductName}', Price: {item.Price}, Qty: {item.Quantity}");
                }
            }

            HttpContext.Session.SetString("OrderBasket", JsonSerializer.Serialize(basket2));

            // Sipariş oluştur
            var orderResult = await _orderService.CreateOrder(checkoutInfoInput);

            if (!orderResult.IsSuccessful)
            {
                var basket = await _basketService.GetBasketAsync();
                var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
                var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

                ViewBag.Basket = basket;
                ViewBag.DeliveryAddresses = deliveryAddresses;
                ViewBag.InvoiceAddresses = invoiceAddresses;
                ViewBag.Error = orderResult.Error;

                return View(checkoutInfoInput);
            }

            _logger.LogInformation($"[OrderController] Order created: {orderResult.OrderId}");

            // Adresi kaydet (RabbitMQ / Event-Driven)
            if (checkoutInfoInput.SaveAddress)
            {
                _logger.LogInformation($"[OrderController] SaveAddress is true. Publishing UserAddressSaveRequested for user: {basket2.UserId}");

                var addressEvent = new UserAddressSaveRequested
                {
                    UserId = basket2.UserId,
                    Type = 1, // Delivery
                    Title = checkoutInfoInput.AddressTitle,
                    FirstName = checkoutInfoInput.CustomerName,
                    LastName = checkoutInfoInput.CustomerSurname,
                    PhoneNumber = checkoutInfoInput.CustomerPhone,
                    Email = checkoutInfoInput.CustomerEmail,
                    Province = checkoutInfoInput.Province,
                    District = checkoutInfoInput.District,
                    Neighborhood = checkoutInfoInput.Street,
                    PostalCode = checkoutInfoInput.ZipCode,
                    AddressDetail = checkoutInfoInput.Line
                };

                await _publishEndpoint.Publish(addressEvent);
            }

            // Checkout bilgilerini session'a kaydet (Payment sayfasında kullanılacak)
            HttpContext.Session.SetString("CheckoutInfo", JsonSerializer.Serialize(checkoutInfoInput));

            // Ödeme sayfasına yönlendir
            return RedirectToAction("Payment", new { orderId = orderResult.OrderId });
        }

        public async Task<IActionResult> Payment(int orderId)
        {
            var checkoutInfoJson = HttpContext.Session.GetString("CheckoutInfo");
            var basketJson = HttpContext.Session.GetString("OrderBasket");

            if (string.IsNullOrEmpty(checkoutInfoJson) || string.IsNullOrEmpty(basketJson))
            {
                return RedirectToAction("Index", "Home");
            }

            var checkoutInfo = JsonSerializer.Deserialize<CheckoutInfoInput>(checkoutInfoJson);
            var basket = JsonSerializer.Deserialize<Models.Baskets.BasketViewModel>(basketJson);

            if (checkoutInfo == null || basket == null)
            {
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation($"[OrderController] Basket from session:");
            _logger.LogInformation($"  - UserId: {basket.UserId}");
            _logger.LogInformation($"  - Items Count: {basket.Items?.Count ?? 0}");
            if (basket.Items != null)
            {
                foreach (var item in basket.Items)
                {
                    _logger.LogInformation($"    * ProductId: {item.ProductId}, ProductName: '{item.ProductName}', Price: {item.Price}, Qty: {item.Quantity}");
                }
            }

            // Ödeme isteği oluştur
            var expiration = checkoutInfo.Expiration.Split('/');
            var paymentRequest = new PaymentRequest
            {
                OrderId = orderId,
                CardName = checkoutInfo.CardName,
                CardNumber = checkoutInfo.CardNumber?.Replace(" ", "") ?? string.Empty,
                ExpireMonth = expiration.Length > 0 ? expiration[0] : "12",
                ExpireYear = expiration.Length > 1 ? "20" + expiration[1] : "2030",
                CVV = checkoutInfo.CVV,
                TotalPrice = basket.TotalPrice,
                CustomerName = checkoutInfo.CustomerName,
                CustomerSurname = checkoutInfo.CustomerSurname,
                CustomerEmail = checkoutInfo.CustomerEmail,
                CustomerPhone = checkoutInfo.CustomerPhone,
                AddressDetail = $"{checkoutInfo.Street} {checkoutInfo.Line}",
                City = checkoutInfo.Province,
                ZipCode = checkoutInfo.ZipCode,
                Items = basket.Items.Select(x => new PaymentItem
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    Price = x.Price * x.Quantity
                }).ToList()
            };

            _logger.LogInformation($"[OrderController] Payment request created:");
            _logger.LogInformation($"  - OrderId: {paymentRequest.OrderId}");
            _logger.LogInformation($"  - TotalPrice: {paymentRequest.TotalPrice}");
            _logger.LogInformation($"  - Customer: {paymentRequest.CustomerName} {paymentRequest.CustomerSurname}");
            _logger.LogInformation($"  - Email: {paymentRequest.CustomerEmail}");
            _logger.LogInformation($"  - Items Count: {paymentRequest.Items.Count}");
            foreach (var item in paymentRequest.Items)
            {
                _logger.LogInformation($"    * {item.ProductName} - {item.Price} TL");
            }

            // Ödeme işlemini gerçekleştir
            var paymentResult = await _paymentService.ProcessPayment(paymentRequest);

            // Session'ı temizle
            HttpContext.Session.Remove("CheckoutInfo");
            HttpContext.Session.Remove("OrderBasket");

            if (paymentResult.Success)
            {
                // Sepeti temizle
                await _basketService.DeleteAsync();
                _logger.LogInformation($"[OrderController] Basket cleared after successful payment");

                return RedirectToAction("Success", new { orderId });
            }
            else
            {
                ViewBag.Error = paymentResult.Message;
                ViewBag.OrderId = orderId;
                return View();
            }
        }

        public IActionResult Success(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }

        public async Task<IActionResult> History()
        {
            var orders = await _orderService.GetOrders();
            return View(orders);
        }
    }
}
