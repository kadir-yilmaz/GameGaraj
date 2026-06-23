using GameGaraj.Shared.Events;
using GameGaraj.WebUI.Models.Campaigns;
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
        private readonly ICatalogService _catalogService;
        private readonly ICampaignService _campaignService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IBasketService basketService,
            IOrderService orderService,
            IPaymentService paymentService,
            ICatalogService catalogService,
            ICampaignService campaignService,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderController> _logger)
        {
            _basketService = basketService;
            _orderService = orderService;
            _paymentService = paymentService;
            _catalogService = catalogService;
            _campaignService = campaignService;
            _publishEndpoint = publishEndpoint;
            this._logger = _logger;
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
            await SyncBasketWithCatalogAsync(basket);
            var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
            var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

            ViewBag.Basket = basket;
            ViewBag.DeliveryAddresses = deliveryAddresses;
            ViewBag.InvoiceAddresses = invoiceAddresses;

            await PrepareCheckoutBag(basket);

            // Giriş yapmış kullanıcı bilgilerini ön tanımlı olarak getir
            var model = new CheckoutInfoInput();
            
            if (User.Identity?.IsAuthenticated == true)
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
                _logger.LogWarning("[OrderController] ModelState is INVALID. Details:");
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        _logger.LogWarning($"  --> Field: {entry.Key}, Error: {error.ErrorMessage}");
                    }
                }

                var basket = await _basketService.GetBasketAsync();
                var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
                var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

                ViewBag.Basket = basket;
                ViewBag.DeliveryAddresses = deliveryAddresses;
                ViewBag.InvoiceAddresses = invoiceAddresses;

                // Re-calculate discounts and shipping for error view
                if (basket != null)
                {
                    await SyncBasketWithCatalogAsync(basket);
                    await PrepareCheckoutBag(basket);
                }

                return View(checkoutInfoInput);
            }

            _logger.LogInformation("[OrderController] Processing checkout");
            _logger.LogInformation($"[OrderController] Customer: {checkoutInfoInput.CustomerName} {checkoutInfoInput.CustomerSurname}");
            _logger.LogInformation($"[OrderController] Address: {checkoutInfoInput.Province}/{checkoutInfoInput.District}");
            _logger.LogInformation($"[OrderController] Card: {checkoutInfoInput.CardName}");

            // Sepeti session'a kaydet (Payment'ta kullanılacak)
            var basket2 = await _basketService.GetBasketAsync();
            if (basket2 == null || basket2.Items == null || !basket2.Items.Any())
            {
                ViewBag.Error = "Sipariş oluşturulurken sepet bulunamadı.";
                return View(checkoutInfoInput);
            }

            var orderBasket = basket2;

            await SyncBasketWithCatalogAsync(orderBasket);

            _logger.LogInformation($"[OrderController] Basket before session:");
            _logger.LogInformation($"  - UserId: {orderBasket.UserId}");
            _logger.LogInformation($"  - Items Count: {orderBasket.Items.Count}");
            if (orderBasket.Items != null)
            {
                foreach (var item in orderBasket.Items)
                {
                    _logger.LogInformation($"    * ProductId: {item.ProductId}, ProductName: '{item.ProductName}', Price: {item.Price}, Qty: {item.Quantity}");
                }
            }

            HttpContext.Session.SetString("OrderBasket", JsonSerializer.Serialize(orderBasket));

            // Sipariş oluştur
            var pricingSnapshot = await BuildOrderPricingSnapshotAsync(orderBasket);
            var orderResult = await _orderService.CreateOrder(checkoutInfoInput, pricingSnapshot);

            if (!orderResult.IsSuccessful)
            {
                var basket = await _basketService.GetBasketAsync();
                var deliveryAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
                var invoiceAddresses = await _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

                ViewBag.Basket = basket;
                ViewBag.DeliveryAddresses = deliveryAddresses;
                ViewBag.InvoiceAddresses = invoiceAddresses;
                ViewBag.Error = orderResult.Error;

                // Re-calculate discounts and shipping for error view
                if (basket != null)
                {
                    await SyncBasketWithCatalogAsync(basket);
                    await PrepareCheckoutBag(basket);
                }

                return View(checkoutInfoInput);
            }

            _logger.LogInformation($"[OrderController] Order created: {orderResult.OrderId}");

            // Adresi kaydet (RabbitMQ / Event-Driven)
            if (checkoutInfoInput.SaveAddress)
            {
                var basketUserId = orderBasket.UserId ?? string.Empty;
                _logger.LogInformation($"[OrderController] SaveAddress is true. Publishing UserAddressSaveRequested for user: {basketUserId}");

                var addressEvent = new UserAddressSaveRequested
                {
                    UserId = basketUserId,
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

        [HttpPost]
        public IActionResult ApplyCoupon([FromForm] string couponCode)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                TempData["CouponError"] = "Lütfen bir kupon kodu girin.";
                return RedirectToAction("Checkout");
            }

            couponCode = couponCode.ToUpperInvariant();
            HttpContext.Session.SetString("AppliedCouponCode", couponCode);
            
            return RedirectToAction("Checkout");
        }

        [HttpPost]
        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove("AppliedCouponCode");
            return RedirectToAction("Checkout");
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

            basket.Items ??= new List<Models.Baskets.BasketItemViewModel>();
            var basketItems = basket.Items;

            _logger.LogInformation($"[OrderController] Basket from session:");
            _logger.LogInformation($"  - UserId: {basket.UserId}");
            _logger.LogInformation($"  - Items Count: {basketItems.Count}");
            if (basketItems.Count > 0)
            {
                foreach (var item in basketItems)
                {
                    _logger.LogInformation($"    * ProductId: {item.ProductId}, ProductName: '{item.ProductName}', Price: {item.Price}, Qty: {item.Quantity}");
                }
            }

            // Aktif kampanyayı ve kargo ayarlarını tekrar hesapla ki gerçek ödenecek tutar iyzico'ya gitsin
            var pricingSnapshot = await BuildOrderPricingSnapshotAsync(basket);

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
                TotalPrice = pricingSnapshot.TotalPaidAmount,
                CustomerName = checkoutInfo.CustomerName,
                CustomerSurname = checkoutInfo.CustomerSurname,
                CustomerEmail = checkoutInfo.CustomerEmail,
                CustomerPhone = checkoutInfo.CustomerPhone,
                AddressDetail = $"{checkoutInfo.Street} {checkoutInfo.Line}",
                City = checkoutInfo.Province,
                ZipCode = checkoutInfo.ZipCode,
                Items = basketItems.Select(x => new PaymentItem
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
                // Kupon kullanıldıysa DB'de güncelle
                var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
                if (!string.IsNullOrEmpty(couponCode))
                {
                    await _campaignService.MarkCouponAsUsedAsync(couponCode);
                    _logger.LogInformation($"[OrderController] Coupon {couponCode} marked as used in DB.");
                }
                HttpContext.Session.Remove("AppliedCouponCode");

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

        private async Task PrepareCheckoutBag(Models.Baskets.BasketViewModel basket)
        {
            await SyncBasketWithCatalogAsync(basket);

            // Kampanya indirim hesaplama
            try
            {
                var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var discountRequest = new CalculateDiscountRequest
                {
                    Items = basket.Items.Select(i => new OrderItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        CategoryId = i.CategoryId,
                        Brand = i.Brand,
                        UnitPrice = i.Price,
                        Quantity = i.Quantity
                    }).ToList(),
                    CouponCode = couponCode,
                    UserId = currentUserId
                };

                var discountResult = await _campaignService.CalculateDiscountAsync(discountRequest);
                ViewBag.DiscountResult = discountResult;

                if (!string.IsNullOrEmpty(couponCode) && discountResult != null && !discountResult.IsCouponApplied)
                {
                    HttpContext.Session.Remove("AppliedCouponCode");
                    TempData["CouponError"] = discountResult.CouponMessage ?? "Kupon geçersiz.";
                }
                else if (!string.IsNullOrEmpty(couponCode) && discountResult != null && discountResult.IsCouponApplied)
                {
                    TempData["CouponSuccess"] = discountResult.CouponMessage ?? "Kupon uygulandı.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OrderController] Kampanya indirimi hesaplanamadı.");
            }

            // Kargo ayarlarını çek
            var shippingSetting = await _campaignService.GetShippingSettingAsync();
            if (shippingSetting == null)
            {
                shippingSetting = new ShippingSettingViewModel
                {
                    FreeShippingThreshold = 500,
                    DefaultShippingFee = 0,
                    IsActive = false
                };
            }
            ViewBag.ShippingSetting = shippingSetting;
        }

        private async Task<OrderPricingSnapshot> BuildOrderPricingSnapshotAsync(Models.Baskets.BasketViewModel basket)
        {
            await SyncBasketWithCatalogAsync(basket);

            basket.Items ??= new List<Models.Baskets.BasketItemViewModel>();
            var basketItems = basket.Items;

            var snapshot = new OrderPricingSnapshot
            {
                OriginalTotalAmount = basket.TotalPrice,
                TotalPaidAmount = basket.TotalPrice
            };

            var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var discountRequest = new CalculateDiscountRequest
            {
                Items = basketItems.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    CategoryId = i.CategoryId,
                    Brand = i.Brand,
                    UnitPrice = i.Price,
                    Quantity = i.Quantity
                }).ToList(),
                CouponCode = couponCode,
                UserId = currentUserId
            };

            var discountResult = await _campaignService.CalculateDiscountAsync(discountRequest);
            if (discountResult != null)
            {
                snapshot.CampaignDiscountAmount = discountResult.TotalDiscount;
                snapshot.AppliedCampaignName = discountResult.AppliedRuleName;
                snapshot.TotalPaidAmount = discountResult.FinalTotal;

                if (discountResult.AppliedRules != null)
                {
                    snapshot.OrderPricingLedgers = discountResult.AppliedRules.Select(r => new OrderPricingLedgerViewModel
                    {
                        Title = r.RuleName,
                        Amount = r.DiscountAmount,
                        Type = 1 // Discount
                    }).ToList();
                }

                if (discountResult.IsCouponApplied)
                {
                    snapshot.CouponCode = couponCode;
                }
            }

            var shippingSetting = await _campaignService.GetShippingSettingAsync();
            if (shippingSetting != null && shippingSetting.IsActive && basket.TotalPrice < shippingSetting.FreeShippingThreshold)
            {
                snapshot.ShippingFee = shippingSetting.DefaultShippingFee;
                // Eğer kupon kargo bedava ise ve discountResult içinde bu uygulanmışsa, 
                // FinalTotal'a ShippingFee eklenmemesi veya OrderPricingLedgers'da düşülmesi gerekir.
                // CampaignCalculationService Kargo Bedava uyguladığında CouponMessage döner.
                if (discountResult != null && discountResult.IsCouponApplied && discountResult.CouponMessage == "Kargo Bedava kuponu uygulandı.")
                {
                    snapshot.ShippingFee = 0;
                }
                snapshot.TotalPaidAmount += snapshot.ShippingFee;
            }

            return snapshot;
        }

        private async Task SyncBasketWithCatalogAsync(Models.Baskets.BasketViewModel basket)
        {
            basket.Items ??= new List<Models.Baskets.BasketItemViewModel>();

            var needsSave = false;
            foreach (var item in basket.Items)
            {
                var product = await _catalogService.GetProductByIdAsync(item.ProductId);
                if (product == null)
                {
                    continue;
                }

                if (item.Price != product.Price)
                {
                    item.Price = product.Price;
                    needsSave = true;
                }

                if (string.IsNullOrWhiteSpace(item.CategoryId) && !string.IsNullOrWhiteSpace(product.CategoryId))
                {
                    item.CategoryId = product.CategoryId;
                    needsSave = true;
                }

                if (string.IsNullOrWhiteSpace(item.Brand) && !string.IsNullOrWhiteSpace(product.Brand))
                {
                    item.Brand = product.Brand;
                    needsSave = true;
                }
            }

            if (needsSave && !string.IsNullOrWhiteSpace(basket.UserId))
            {
                await _basketService.SaveOrUpdateAsync(basket);
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyCoupons()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("SignIn", "Auth");
            }
            var coupons = await _campaignService.GetUserCouponsAsync(userId);
            return View(coupons);
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new List<NotificationViewModel>());
            }
            var notifications = await _campaignService.GetNotificationsAsync(userId);
            return Json(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var success = await _campaignService.MarkNotificationAsReadAsync(id);
            return Json(new { success });
        }
    }
}
