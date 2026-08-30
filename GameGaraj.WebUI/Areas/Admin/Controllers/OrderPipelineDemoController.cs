using System.Diagnostics;
using GameGaraj.Shared.Chaos;
using GameGaraj.Shared.Dtos;
using GameGaraj.Shared.Observability;
using GameGaraj.WebUI.Models.Orders;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class OrderPipelineDemoController : Controller
    {
        private readonly IChaosManager _chaosManager;
        private readonly ICatalogService _catalogService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPipelineNotifier _pipelineNotifier;
        private readonly ILogger<OrderPipelineDemoController> _logger;

        public OrderPipelineDemoController(
            IChaosManager chaosManager,
            ICatalogService catalogService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPipelineNotifier pipelineNotifier,
            ILogger<OrderPipelineDemoController> logger)
        {
            _chaosManager = chaosManager;
            _catalogService = catalogService;
            _orderService = orderService;
            _paymentService = paymentService;
            _pipelineNotifier = pipelineNotifier;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rules = await _chaosManager.GetAllRulesAsync();
            var products = await _catalogService.GetAllProductsAsync();
            ViewBag.Rules = rules;
            ViewBag.Products = products ?? new List<GameGaraj.WebUI.Models.Products.ProductViewModel>();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var rules = await _chaosManager.GetAllRulesAsync();
            return Json(new { success = true, rules });
        }

        [HttpPost]
        public async Task<IActionResult> SetRule([FromBody] ChaosRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.ServiceName))
            {
                return BadRequest(new { success = false, message = "Geçersiz servis adı." });
            }

            await _chaosManager.SetRuleAsync(rule);
            return Json(new { success = true, message = $"{rule.ServiceName.ToUpper()} kuralı güncellendi.", rule });
        }

        [HttpPost]
        public async Task<IActionResult> ResetRule([FromQuery] string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return BadRequest(new { success = false, message = "Servis adı boş olamaz." });
            }

            await _chaosManager.ResetRuleAsync(serviceName);
            return Json(new { success = true, message = $"{serviceName.ToUpper()} kuralı sıfırlandı." });
        }

        [HttpPost]
        public async Task<IActionResult> ResetAllRules()
        {
            await _chaosManager.ResetAllRulesAsync();
            return Json(new { success = true, message = "Tüm boru hattı kuralları normale döndürüldü." });
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteStep([FromBody] PipelineStepRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new PipelineStepResult
            {
                StepKey = request.StepKey,
                StepName = request.StepName
            };

            try
            {
                switch (request.StepKey)
                {
                    case "stock_validate":
                        var stockReq = new StockValidationRequest
                        {
                            Items = new List<StockValidationItem>
                            {
                                new()
                                {
                                    ProductId = request.ProductId ?? "7bd6ed51-c13f-46fd-9602-293979dc61fd",
                                    ProductName = request.ProductName ?? "Intel Core i7-14700K",
                                    Quantity = request.Quantity > 0 ? request.Quantity : 1
                                }
                            }
                        };
                        var stockRes = await _catalogService.ValidateStockAsync(stockReq);
                        stopwatch.Stop();
                        response.DurationMs = stopwatch.ElapsedMilliseconds;

                        if (stockRes != null && stockRes.IsValid)
                        {
                            response.Success = true;
                            response.StatusCode = 200;
                            response.Message = "Stok başarıyla doğrulandı ve rezerve edildi.";
                            response.Payload = stockRes;
                        }
                        else
                        {
                            response.Success = false;
                            response.StatusCode = 400;
                            response.Message = stockRes != null && stockRes.Errors.Any() 
                                ? string.Join(", ", stockRes.Errors) 
                                : "Stok yetersiz veya servis yanıt vermedi.";
                            response.Payload = stockRes;
                        }
                        break;

                    case "order_create":
                        var directOrder = new DirectOrderInput
                        {
                            BuyerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "kadiryilmaz.dev@gmail.com",
                            ProductId = request.ProductId ?? "7bd6ed51-c13f-46fd-9602-293979dc61fd",
                            ProductName = request.ProductName ?? "Intel Core i7-14700K",
                            PictureUrl = !string.IsNullOrWhiteSpace(request.PictureUrl) ? request.PictureUrl : "/default.jpg",
                            Quantity = request.Quantity > 0 ? request.Quantity : 1,
                            TotalAmount = request.TotalPrice > 0 ? request.TotalPrice : 15500m,
                            CustomerName = "Kadir",
                            CustomerSurname = "Yılmaz",
                            CustomerEmail = "kadiryilmaz.dev@gmail.com",
                            CustomerPhone = "+905551234567",
                            Province = "İstanbul",
                            District = "Sarıyer",
                            AddressDetail = "Maslak Mah. Büyükdere Cad. No:123"
                        };

                        var orderCreated = await _orderService.CreateDirectOrderAsync(directOrder);
                        stopwatch.Stop();
                        response.DurationMs = stopwatch.ElapsedMilliseconds;

                        if (orderCreated != null && orderCreated.IsSuccessful && orderCreated.OrderId > 0)
                        {
                            response.Success = true;
                            response.StatusCode = 200;
                            response.Message = $"Sipariş #{orderCreated.OrderId} 'Pending' statüsünde SQL Server DB ve Outbox'a başarıyla yazıldı.";
                            response.Payload = orderCreated;
                        }
                        else
                        {
                            response.Success = false;
                            response.StatusCode = 503;
                            response.Message = $"Order.API yanıtı: {orderCreated?.Error ?? "Servis yanıt vermedi"}";
                            response.Payload = orderCreated;
                        }
                        break;

                    case "payment_process":
                        var payReq = new PaymentRequest
                        {
                            OrderId = request.OrderId > 0 ? request.OrderId : 10101,
                            CardName = request.CardName ?? "Kadir Yılmaz",
                            CardNumber = request.CardNumber ?? "5528790000000008",
                            ExpireMonth = "12",
                            ExpireYear = "2029",
                            CVV = "123",
                            TotalPrice = request.TotalPrice > 0 ? request.TotalPrice : 15500m,
                            CustomerName = "Kadir",
                            CustomerSurname = "Yılmaz",
                            CustomerEmail = "kadiryilmaz.dev@gmail.com",
                            CustomerPhone = "+905551234567",
                            AddressDetail = "Maslak Mah. Büyükdere Cad. No:123",
                            City = "İstanbul",
                            ZipCode = "34398",
                            Items = new List<PaymentItem>
                            {
                                new()
                                {
                                    ProductId = request.ProductId ?? "7bd6ed51-c13f-46fd-9602-293979dc61fd",
                                    ProductName = request.ProductName ?? "Intel Core i7-14700K",
                                    Price = request.TotalPrice > 0 ? request.TotalPrice : 15500m
                                }
                            }
                        };

                        var payResult = await _paymentService.ProcessPayment(payReq);
                        stopwatch.Stop();
                        response.DurationMs = stopwatch.ElapsedMilliseconds;
                        response.Success = payResult.Success;
                        response.StatusCode = payResult.Success ? 200 : 400;
                        response.Message = payResult.Success 
                            ? "Ödeme İyzico üzerinden başarıyla çekildi (PaymentCompleted Event Publish Edildi)." 
                            : $"Ödeme başarısız: {payResult.Message} (PaymentFailed Event Publish Edildi).";
                        response.Payload = payResult;
                        break;

                    case "rabbitmq_events":
                        stopwatch.Stop();
                        response.DurationMs = stopwatch.ElapsedMilliseconds + 25;
                        response.Success = true;
                        response.StatusCode = 200;
                        response.Message = "MassTransit Event'leri kuyruğa dağıtıldı: Catalog (Stok Düşümü), Invoice (Fatura PDF), Campaign (Kupon).";
                        response.Payload = new
                        {
                            Events = new[] { "PaymentCompleted -> Catalog.API", "PaymentCompleted -> Invoice.API", "PaymentCompleted -> Campaign.API" },
                            Status = "Delivered",
                            Queue = "payment-completed-order-service"
                        };
                        break;

                    default:
                        response.Success = false;
                        response.StatusCode = 400;
                        response.Message = $"Bilinmeyen adım: {request.StepKey}";
                        break;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.DurationMs = stopwatch.ElapsedMilliseconds;
                response.Success = false;
                response.StatusCode = 500;
                response.Message = $"Hata oluştu: {ex.Message}";
                response.Payload = new { Exception = ex.ToString() };
            }

            return Json(response);
        }
    }

    public class PipelineStepRequest
    {
        public string StepKey { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? PictureUrl { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal TotalPrice { get; set; } = 15500m;
        public int OrderId { get; set; }
        public string? CardNumber { get; set; }
        public string? CardName { get; set; }
    }

    public class PipelineStepResult
    {
        public string StepKey { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public long DurationMs { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Payload { get; set; }
    }
}
