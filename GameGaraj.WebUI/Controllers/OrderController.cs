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
        private readonly IPipelineNotifier _pipelineNotifier;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IBasketService basketService,
            IOrderService orderService,
            IPaymentService paymentService,
            ICatalogService catalogService,
            ICampaignService campaignService,
            IReviewService reviewService,
            IPublishEndpoint publishEndpoint,
            IPipelineNotifier pipelineNotifier,
            ILogger<OrderController> _logger)
        {
            _basketService = basketService;
            _orderService = orderService;
            _paymentService = paymentService;
            _catalogService = catalogService;
            _campaignService = campaignService;
            _reviewService = reviewService;
            _publishEndpoint = publishEndpoint;
            _pipelineNotifier = pipelineNotifier;
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

            // Kayıtlı adresleri ve sepet senkronizasyonunu paralel getir
            var syncTask = SyncBasketWithCatalogAsync(basket);
            var deliveryTask = _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Delivery);
            var invoiceTask = _orderService.GetUserAddressesAsync(Models.Addresses.AddressType.Invoice);

            await Task.WhenAll(syncTask, deliveryTask, invoiceTask);

            ViewBag.Basket = basket;
            ViewBag.DeliveryAddresses = deliveryTask.Result ?? new List<Models.Addresses.UserAddressViewModel>();
            ViewBag.InvoiceAddresses = invoiceTask.Result ?? new List<Models.Addresses.UserAddressViewModel>();

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

            // Auto-fill CustomerEmail if not populated from form
            if (string.IsNullOrWhiteSpace(checkoutInfoInput.CustomerEmail))
            {
                checkoutInfoInput.CustomerEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value 
                                                ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value 
                                                ?? User.Identity?.Name 
                                                ?? "kadiryilmaz.dev@gmail.com";
                ModelState.Remove(nameof(checkoutInfoInput.CustomerEmail));
            }

            if (string.IsNullOrWhiteSpace(checkoutInfoInput.CustomerName))
            {
                checkoutInfoInput.CustomerName = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.GivenName)?.Value 
                                               ?? User.Claims.FirstOrDefault(x => x.Type == "name")?.Value ?? "Kadir";
                ModelState.Remove(nameof(checkoutInfoInput.CustomerName));
            }

            if (string.IsNullOrWhiteSpace(checkoutInfoInput.CustomerSurname))
            {
                checkoutInfoInput.CustomerSurname = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value ?? "Yılmaz";
                ModelState.Remove(nameof(checkoutInfoInput.CustomerSurname));
            }

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

            // SignalR Canlı Log & Zaman Sayacı Başlangıcı
            var checkoutStartTime = DateTime.UtcNow;
            HttpContext.Session.SetString("CheckoutStartTime", checkoutStartTime.ToString("o"));
            await _pipelineNotifier.NotifyAsync("WebUI (Gerçek Checkout)", $"🛒 Müşteri '{checkoutInfoInput.CustomerEmail}' sipariş adımını başlattı ({orderBasket.Items.Count} kalem ürün).", "info", step: 1);

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

                await _pipelineNotifier.NotifyAsync("Order.API", $"📝 [Adım 2/4] Sipariş oluşturuluyor ve Outbox tablosuna yazılıyor (Tutar: ₺{pricingSnapshot.TotalPaidAmount:N2})...", "info", step: 2);

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

                    await _pipelineNotifier.NotifyAsync("Order.API", $"❌ [Adım 2/4] Sipariş oluşturulamadı: {orderResult.Error}", "error", step: 2);

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
                await _pipelineNotifier.NotifyAsync("Order.API", $"✅ [Adım 2/4] Sipariş #{orderResult.OrderId} 'Pending' statüsünde DB & Outbox'a başarıyla yazıldı.", "success", step: 2);

                // Adresi kaydet (RabbitMQ / Event-Driven)
                if (checkoutInfoInput.SaveAddress)
                {
                    var basketUserId = orderBasket.UserId ?? string.Empty;
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

                // Aktif kampanyayı ve kargo ayarlarını tekrar hesapla
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
                    ExpireYear = expiration.Length > 1 ? "20" + expiration[1] : "2029",
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

                // 0. Stok Ön-Doğrulaması (Saga Guard - Ödeme öncesi senkron stok teyidi)
                await _pipelineNotifier.NotifyAsync("Catalog.API", $"💧 [Adım 1/4] Sepetteki {basketItems.Count} kalem ürün için stok doğrulaması isteniyor...", "info", step: 1);

                using (var stockValidateActivity = AppDiagnostics.StartActivity("Validate Stock Before Payment"))
                {
                    stockValidateActivity?.SetTag("order.id", orderId);
                    var stockValidationRequest = new GameGaraj.Shared.Dtos.StockValidationRequest
                    {
                        Items = basketItems.Select(x => new GameGaraj.Shared.Dtos.StockValidationItem
                        {
                            ProductId = x.ProductId,
                            ProductName = x.ProductName,
                            Quantity = x.Quantity
                        }).ToList()
                    };

                    var stockValidationResult = await _catalogService.ValidateStockAsync(stockValidationRequest);
                    if (stockValidationResult != null && !stockValidationResult.IsValid)
                    {
                        var stockErrorMessage = string.Join(" | ", stockValidationResult.Errors);
                        _logger.LogWarning($"[OrderController] Stock validation failed before payment for Order #{orderId}: {stockErrorMessage}");
                        stockValidateActivity?.SetStatus(ActivityStatusCode.Error, stockErrorMessage);

                        // Süre hesapla
                        var elapsed = GetTotalOrderDuration();
                        await _pipelineNotifier.NotifyAsync("Catalog.API", $"❌ [Adım 1/4] SAGA KORUMASI DEVREDE: {stockErrorMessage}. Karttan para çekilmedi!", "error", step: 1);
                        await _pipelineNotifier.NotifyAsync("Sipariş Sonucu", $"💥 [Sipariş #{orderId} İptal] Stok teyit edilemediği için işlem durduruldu. Toplam Süre: ⏱️ {elapsed.TotalSeconds:F2} sn ({elapsed.TotalMilliseconds:N0} ms)", "error");

                        ViewBag.Error = $"Ödeme yapılamadı: {stockErrorMessage}";
                        ViewBag.OrderId = orderId;
                        return View();
                    }
                }

                await _pipelineNotifier.NotifyAsync("Catalog.API", "✅ [Adım 1/4] Stok başarıyla doğrulandı & rezerve edildi.", "success", step: 1);

                // Ödeme işlemini gerçekleştir
                await _pipelineNotifier.NotifyAsync("Payment.API", $"💳 [Adım 3/4] İyzico Gateway'e ödeme isteği gönderiliyor (Tutar: ₺{paymentRequest.TotalPrice:N2}, Sipariş #{orderId})...", "info", step: 3);

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

                // Toplam sipariş süresini hesapla
                var totalDuration = GetTotalOrderDuration();

                // Session'ı temizle
                HttpContext.Session.Remove("CheckoutInfo");
                HttpContext.Session.Remove("OrderBasket");
                HttpContext.Session.Remove("CheckoutStartTime");

                if (paymentResult.Success)
                {
                    paymentActivity?.SetTag("payment.status", "Success");

                    await _pipelineNotifier.NotifyAsync("Payment.API", "✅ [Adım 3/4] Ödeme Başarıyla Alındı! PaymentCompletedEvent fırlatıldı.", "success", step: 3);
                    await _pipelineNotifier.NotifyAsync("RabbitMQ (Outbox)", "⚡ [Adım 4/4] MassTransit Saga Dağıtımı: Stok Düşümü (Catalog.API), PDF Fatura (Invoice.API), Kupon Kazanımı (Campaign.API) işleniyor...", "success", step: 4);
                    await _pipelineNotifier.NotifyAsync("Sipariş Sonucu", $"🏁 [Sipariş #{orderId} Başarılı] Sipariş ve ödeme başarıyla tamamlandı! Toplam İşlem Süresi: ⏱️ {totalDuration.TotalSeconds:F2} saniye ({totalDuration.TotalMilliseconds:N0} ms)", "success", step: 4);

                    // Kupon kullanıldıysa DB'de güncelle
                    var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
                    if (!string.IsNullOrEmpty(couponCode))
                    {
                        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!string.IsNullOrWhiteSpace(currentUserId))
                        {
                            await _campaignService.MarkCouponAsUsedAsync(couponCode, currentUserId);
                        }
                    }
                    HttpContext.Session.Remove("AppliedCouponCode");

                    // Sepeti temizle
                    await _basketService.DeleteAsync();

                    return RedirectToAction("Success", new { orderId });
                }
                else
                {
                    paymentActivity?.SetTag("payment.status", "Failed");
                    paymentActivity?.SetStatus(ActivityStatusCode.Error, paymentResult.Message);

                    await _pipelineNotifier.NotifyAsync("Payment.API", $"❌ [Adım 3/4] Ödeme Başarısız ({paymentResult.Message})! PaymentFailedEvent fırlatıldı (Saga Rollback: Rezerve stok iade edilecek).", "error", step: 3);
                    await _pipelineNotifier.NotifyAsync("Sipariş Sonucu", $"💥 [Sipariş #{orderId} İptal] Kart/Banka ödeme reddi: {paymentResult.Message}. Toplam Geçen Süre: ⏱️ {totalDuration.TotalSeconds:F2} saniye ({totalDuration.TotalMilliseconds:N0} ms)", "error", step: 3);

                    ViewBag.Error = paymentResult.Message;
                    ViewBag.OrderId = orderId;
                    return View();
                }
            }
        }

        private TimeSpan GetTotalOrderDuration()
        {
            var startTimeStr = HttpContext.Session.GetString("CheckoutStartTime");
            if (!string.IsNullOrEmpty(startTimeStr) && DateTime.TryParse(startTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startTime))
            {
                return DateTime.UtcNow - startTime;
            }
            return TimeSpan.FromSeconds(0.5);
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

                var discountTask = _campaignService.CalculateDiscountAsync(discountRequest);
                var publicCouponsTask = !string.IsNullOrEmpty(currentUserId)
                    ? _campaignService.GetPublicCouponsAsync(currentUserId)
                    : _campaignService.GetPublicCouponsAsync();
                var userCouponsTask = !string.IsNullOrEmpty(currentUserId)
                    ? _campaignService.GetUserCouponsAsync(currentUserId)
                    : Task.FromResult(new List<CouponViewModel>());
                var shippingTask = _campaignService.GetShippingSettingAsync();

                await Task.WhenAll(discountTask, publicCouponsTask, userCouponsTask, shippingTask);

                var discountResult = discountTask.Result;
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

                ViewBag.PublicCoupons = publicCouponsTask.Result ?? new List<CouponViewModel>();
                ViewBag.UserCoupons = userCouponsTask.Result ?? new List<CouponViewModel>();
                ViewBag.ShippingSetting = shippingTask.Result ?? new ShippingSettingViewModel
                {
                    FreeShippingThreshold = 500,
                    DefaultShippingFee = 0,
                    IsActive = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OrderController] Kampanya indirimi hesaplanamadı.");
                ViewBag.PublicCoupons = new List<CouponViewModel>();
                ViewBag.UserCoupons = new List<CouponViewModel>();
                ViewBag.ShippingSetting = new ShippingSettingViewModel
                {
                    FreeShippingThreshold = 500,
                    DefaultShippingFee = 0,
                    IsActive = false
                };
            }
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

            var discountTask = _campaignService.CalculateDiscountAsync(discountRequest);
            var shippingTask = _campaignService.GetShippingSettingAsync();

            await Task.WhenAll(discountTask, shippingTask);

            var discountResult = discountTask.Result;
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

            var shippingSetting = shippingTask.Result;
            if (shippingSetting != null && shippingSetting.IsActive && basket.TotalPrice < shippingSetting.FreeShippingThreshold)
            {
                snapshot.ShippingFee = shippingSetting.DefaultShippingFee;
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
            if (basket?.Items == null || !basket.Items.Any()) return;

            var tasks = basket.Items.Select(async item => new
            {
                Item = item,
                Product = await _catalogService.GetProductByIdAsync(item.ProductId)
            });

            var results = await Task.WhenAll(tasks);
            var needsSave = false;

            foreach (var r in results)
            {
                if (r.Product == null) continue;

                if (r.Item.Price != r.Product.Price)
                {
                    r.Item.Price = r.Product.Price;
                    needsSave = true;
                }

                if (string.IsNullOrWhiteSpace(r.Item.CategoryId) && !string.IsNullOrWhiteSpace(r.Product.CategoryId))
                {
                    r.Item.CategoryId = r.Product.CategoryId;
                    needsSave = true;
                }

                if (string.IsNullOrWhiteSpace(r.Item.Brand) && !string.IsNullOrWhiteSpace(r.Product.Brand))
                {
                    r.Item.Brand = r.Product.Brand;
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
            var publicCoupons = await _campaignService.GetPublicCouponsAsync(userId);
            var userCoupons = await _campaignService.GetUserCouponsAsync(userId);

            ViewBag.PublicCoupons = publicCoupons;
            return View(userCoupons);
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
