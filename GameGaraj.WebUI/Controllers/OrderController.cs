using System.Diagnostics;
using GameGaraj.Shared.Events;
using GameGaraj.Shared.Observability;
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
        private readonly IReviewService _reviewService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IBasketService basketService,
            IOrderService orderService,
            IPaymentService paymentService,
            ICatalogService catalogService,
            ICampaignService campaignService,
            IReviewService reviewService,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderController> _logger)
        {
            _basketService = basketService;
            _orderService = orderService;
            _paymentService = paymentService;
            _catalogService = catalogService;
            _campaignService = campaignService;
            _reviewService = reviewService;
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

            using (var activity = AppDiagnostics.StartActivity("Create Order"))
            {
                activity?.SetTag("user.id", orderBasket.UserId);
                activity?.SetTag("saga.step", "CreateOrder");
                activity?.SetTag("saga.status", "Started");

                // Sipariş oluştur
                OrderPricingSnapshot pricingSnapshot;
                using (var pricingActivity = AppDiagnostics.StartActivity("Build Order Pricing Snapshot"))
                {
                    pricingActivity?.SetTag("user.id", orderBasket.UserId);
                    pricingActivity?.SetTag("basket.items.count", orderBasket.Items?.Count ?? 0);

                    pricingSnapshot = await BuildOrderPricingSnapshotAsync(orderBasket);

                    pricingActivity?.SetTag("order.original_total", pricingSnapshot.OriginalTotalAmount);
                    pricingActivity?.SetTag("order.total_paid", pricingSnapshot.TotalPaidAmount);
                    pricingActivity?.SetTag("order.campaign_discount", pricingSnapshot.CampaignDiscountAmount);
                    pricingActivity?.SetTag("order.shipping_fee", pricingSnapshot.ShippingFee);
                }

                OrderCreatedViewModel orderResult;
                using (var orderApiActivity = AppDiagnostics.StartActivity("Call Order API"))
                {
                    orderApiActivity?.SetTag("user.id", orderBasket.UserId);
                    orderApiActivity?.SetTag("order.total_paid", pricingSnapshot.TotalPaidAmount);

                    orderResult = await _orderService.CreateOrder(checkoutInfoInput, pricingSnapshot);

                    orderApiActivity?.SetTag("order.id", orderResult.OrderId);
                    orderApiActivity?.SetTag("order.created", orderResult.IsSuccessful);
                    if (!orderResult.IsSuccessful)
                    {
                        orderApiActivity?.SetStatus(ActivityStatusCode.Error, orderResult.Error);
                    }
                }

                if (!orderResult.IsSuccessful)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, orderResult.Error);
                    activity?.SetTag("saga.status", "Failed");

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

                activity?.SetTag("order.id", orderResult.OrderId);
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

                // Köprüleme Mekanizması: Aktif traceparent'ı Session'a at
                var currentTraceParent = Activity.Current?.Id;
                if (currentTraceParent != null)
                {
                    HttpContext.Session.SetString("ParentTraceParent", currentTraceParent);
                }

                // Ödeme sayfasına yönlendir
                return RedirectToAction("Payment", new { orderId = orderResult.OrderId });
            }
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

            // Köprüleme Mekanizması: Session'dan parent traceparent'ı alıp devam ettir
            var parentTraceParent = HttpContext.Session.GetString("ParentTraceParent");
            Activity? paymentActivity = null;

            if (!string.IsNullOrEmpty(parentTraceParent))
            {
                HttpContext.Session.Remove("ParentTraceParent");
                if (ActivityContext.TryParse(parentTraceParent, null, out var parentContext))
                {
                    paymentActivity = AppDiagnostics.StartActivity("Process Payment", ActivityKind.Internal, parentContext);
                }
            }

            if (paymentActivity == null)
            {
                paymentActivity = AppDiagnostics.StartActivity("Process Payment");
            }

            using (paymentActivity)
            {
                paymentActivity?.SetTag("order.id", orderId);
                paymentActivity?.SetTag("user.id", basket.UserId);
                paymentActivity?.SetTag("saga.step", "PaymentProcessing");

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
                OrderPricingSnapshot pricingSnapshot;
                using (var pricingActivity = AppDiagnostics.StartActivity("Build Payment Pricing Snapshot"))
                {
                    pricingActivity?.SetTag("order.id", orderId);
                    pricingActivity?.SetTag("user.id", basket.UserId);
                    pricingActivity?.SetTag("basket.items.count", basketItems.Count);

                    pricingSnapshot = await BuildOrderPricingSnapshotAsync(basket);

                    pricingActivity?.SetTag("payment.total", pricingSnapshot.TotalPaidAmount);
                    pricingActivity?.SetTag("payment.shipping_fee", pricingSnapshot.ShippingFee);
                    pricingActivity?.SetTag("payment.discount_total", pricingSnapshot.CampaignDiscountAmount + pricingSnapshot.CouponDiscountAmount);
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
                    TotalPrice = pricingSnapshot.TotalPaidAmount,
                    CustomerName = checkoutInfo.CustomerName,
                    CustomerSurname = checkoutInfo.CustomerSurname,
                    CustomerEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value 
                                    ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value 
                                    ?? checkoutInfo.CustomerEmail,
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

                using (var requestActivity = AppDiagnostics.StartActivity("Build Payment Request"))
                {
                    requestActivity?.SetTag("order.id", orderId);
                    requestActivity?.SetTag("payment.total", paymentRequest.TotalPrice);
                    requestActivity?.SetTag("payment.items.count", paymentRequest.Items.Count);
                    requestActivity?.SetTag("payment.provider", "Iyzico");
                }

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
                PaymentResult paymentResult;
                using (var paymentApiActivity = AppDiagnostics.StartActivity("Call Payment API"))
                {
                    paymentApiActivity?.SetTag("order.id", orderId);
                    paymentApiActivity?.SetTag("payment.total", paymentRequest.TotalPrice);

                    paymentResult = await _paymentService.ProcessPayment(paymentRequest);

                    paymentApiActivity?.SetTag("payment.status", paymentResult.Success ? "Success" : "Failed");
                    if (!paymentResult.Success)
                    {
                        paymentApiActivity?.SetStatus(ActivityStatusCode.Error, paymentResult.Message);
                    }
                }

                // Session'ı temizle
                HttpContext.Session.Remove("CheckoutInfo");
                HttpContext.Session.Remove("OrderBasket");

                if (paymentResult.Success)
                {
                    paymentActivity?.SetTag("payment.status", "Success");

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
                    paymentActivity?.SetTag("payment.status", "Failed");
                    paymentActivity?.SetStatus(ActivityStatusCode.Error, paymentResult.Message);

                    ViewBag.Error = paymentResult.Message;
                    ViewBag.OrderId = orderId;
                    return View();
                }
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
            var reviews = await _reviewService.GetMyReviewsAsync();
            ViewBag.ReviewedProductIds = reviews
                .Select(review => review.ProductId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ViewBag.MyReviews = reviews.ToDictionary(r => r.ProductId, r => r, StringComparer.OrdinalIgnoreCase);
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("SignIn", "Auth");
            }

            var notifications = await _campaignService.GetNotificationsAsync(userId);
            
            return View(notifications.OrderByDescending(n => n.CreatedDate).ToList());
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
                    ViewBag.AppliedCoupon = await _campaignService.GetCouponByCodeAsync(couponCode);
                }

                // Fetch public and user-specific coupons
                var publicCoupons = await _campaignService.GetPublicCouponsAsync() ?? new List<CouponViewModel>();
                ViewBag.PublicCoupons = publicCoupons;

                var userCoupons = new List<CouponViewModel>();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    userCoupons = await _campaignService.GetUserCouponsAsync(currentUserId) ?? new List<CouponViewModel>();
                }
                ViewBag.UserCoupons = userCoupons;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OrderController] Kampanya indirimi hesaplanamadı.");
                ViewBag.PublicCoupons = new List<CouponViewModel>();
                ViewBag.UserCoupons = new List<CouponViewModel>();
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

        [HttpPost]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            var success = await _campaignService.MarkAllNotificationsAsReadAsync(userId);
            return Json(new { success });
        }
    }
}
