using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GameGaraj.Payment.API.Models;
using GameGaraj.Payment.API.Settings;
using GameGaraj.Shared.Events;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using System.Globalization;

using System.Diagnostics;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;

namespace GameGaraj.Payment.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly Iyzipay.Options _iyzipayOptions;
        private readonly PaymentMetrics _metrics;

        public PaymentsController(
            IPublishEndpoint publishEndpoint,
            IOptions<IyzipaySettings> iyzipaySettings,
            PaymentMetrics metrics)
        {
            _publishEndpoint = publishEndpoint;
            _metrics = metrics;

            var settings = iyzipaySettings.Value;
            _iyzipayOptions = new Iyzipay.Options
            {
                ApiKey = settings.ApiKey,
                SecretKey = settings.SecretKey,
                BaseUrl = settings.BaseUrl
            };
        }

        [HttpPost]
        public async Task<IActionResult> ReceivePayment(PaymentDto paymentDto)
        {
            Console.WriteLine("[PaymentsController] POST ReceivePayment called.");
            _metrics.PaymentAttempted("Iyzico");
            Console.WriteLine($"[PaymentsController] Received PaymentDto:");
            Console.WriteLine($"  - OrderId: {paymentDto.OrderId}");
            Console.WriteLine($"  - TotalPrice: {paymentDto.TotalPrice}");
            Console.WriteLine($"  - CardNumber: {paymentDto.CardNumber}");
            Console.WriteLine($"  - CardName: {paymentDto.CardName}");
            Console.WriteLine($"  - ExpireMonth: {paymentDto.ExpireMonth}");
            Console.WriteLine($"  - ExpireYear: {paymentDto.ExpireYear}");
            Console.WriteLine($"  - CVV: {paymentDto.CVV}");
            Console.WriteLine($"  - CustomerName: {paymentDto.CustomerName}");
            Console.WriteLine($"  - CustomerSurname: {paymentDto.CustomerSurname}");
            Console.WriteLine($"  - CustomerEmail: {paymentDto.CustomerEmail}");
            Console.WriteLine($"  - CustomerPhone: {paymentDto.CustomerPhone}");
            Console.WriteLine($"  - City: {paymentDto.City}");
            Console.WriteLine($"  - Items Count: {paymentDto.Items?.Count ?? 0}");

            if (paymentDto.Items != null)
            {
                foreach (var item in paymentDto.Items)
                {
                    Console.WriteLine($"    * ProductName: '{item.ProductName}' - Price: {item.Price}");
                }
            }

            using (var activity = AppDiagnostics.StartActivity("Call Iyzico"))
            {
                activity?.SetTag("order.id", paymentDto.OrderId);

                // 💰 0 TL kontrolü - ücretsiz satın alma (iyzico 0 TL kabul etmiyor)
                if (paymentDto.TotalPrice <= 0)
                {
                    activity?.SetTag("payment.provider", "Free");
                    activity?.SetTag("payment.status", "Success");
                    activity?.SetTag("payment.id", "FREE-" + paymentDto.OrderId);

                    Console.WriteLine("[PaymentsController] ✅ FREE PURCHASE - Skipping iyzico");
                    _metrics.PaymentSucceeded("Free");

                    // PaymentCompleted event publish et - Order status güncellenecek
                    await _publishEndpoint.Publish(new PaymentCompleted
                    {
                        OrderId = paymentDto.OrderId,
                        OrderItems = paymentDto.Items?.Select(x => new OrderItemMessage
                        {
                            ProductId = x.ProductId,
                            Quantity = 1
                        }).ToList() ?? new List<OrderItemMessage>()
                    });
                    Console.WriteLine($"[PaymentsController] 📤 PaymentCompleted event published for FREE OrderId: {paymentDto.OrderId}");

                    // Fatura event'i de gönder
                    await PublishInvoiceEvent(paymentDto, 0);

                    return Ok(new
                    {
                        Success = true,
                        Message = "Ücretsiz satın alma başarılı",
                        PaymentId = "FREE-" + paymentDto.OrderId
                    });
                }

                activity?.SetTag("payment.provider", "Iyzico");

                // iyzico ödeme isteği oluştur
                Iyzipay.Model.Payment paymentResult;
                using (var tracker = _metrics.TrackPayment())
                {
                    paymentResult = await ProcessIyzipayPayment(paymentDto);
                }

                if (paymentResult.Status != "success")
                {
                    activity?.SetTag("payment.status", "Failed");
                    activity?.SetStatus(ActivityStatusCode.Error, paymentResult.ErrorMessage ?? "Ödeme başarısız");

                    Console.WriteLine($"[PaymentsController] ❌ Payment YOLUNDA GITMEDI: {paymentResult.ErrorMessage}");
                    _metrics.PaymentFailed(paymentResult.ErrorMessage ?? "Payment failed");

                    // 📤 PaymentFailed event publish et - Order status Failed olacak
                    await _publishEndpoint.Publish(new PaymentFailed
                    {
                        OrderId = paymentDto.OrderId,
                        Reason = paymentResult.ErrorMessage ?? "Ödeme başarısız",
                        OrderItems = paymentDto.Items?.Select(x => new OrderItemMessage
                        {
                            ProductId = x.ProductId,
                            Quantity = 1
                        }).ToList() ?? new List<OrderItemMessage>()
                    });
                    Console.WriteLine($"[PaymentsController] 📤 PaymentFailed event published for OrderId: {paymentDto.OrderId}");

                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Ödeme başarısız.",
                        Error = paymentResult.ErrorMessage,
                        ErrorCode = paymentResult.ErrorCode
                    });
                }

                activity?.SetTag("payment.status", "Success");
                activity?.SetTag("payment.id", paymentResult.PaymentId);

                _metrics.PaymentSucceeded("Iyzipay");

                // 📤 PaymentCompleted event publish et - Order status Completed olacak
                await _publishEndpoint.Publish(new PaymentCompleted
                {
                    OrderId = paymentDto.OrderId,
                    OrderItems = paymentDto.Items?.Select(x => new OrderItemMessage
                    {
                        ProductId = x.ProductId,
                        Quantity = 1
                    }).ToList() ?? new List<OrderItemMessage>()
                });
                Console.WriteLine($"[PaymentsController] 📤 PaymentCompleted event published for OrderId: {paymentDto.OrderId}");

                // 📧 Invoice event'i publish et
                await PublishInvoiceEvent(paymentDto, paymentDto.TotalPrice);

                Console.WriteLine($"[PaymentsController] ✅ Payment SUCCESS - PaymentId: {paymentResult.PaymentId}");
                return Ok(new
                {
                    Success = true,
                    Message = "Ödeme başarılı",
                    PaymentId = paymentResult.PaymentId
                });
            }
        }

        private async Task PublishInvoiceEvent(PaymentDto paymentDto, decimal totalPrice)
        {
            var invoiceEvent = new InvoiceRequested
            {
                OrderId = paymentDto.OrderId,
                CustomerName = $"{paymentDto.CustomerName} {paymentDto.CustomerSurname}",
                CustomerEmail = paymentDto.CustomerEmail,
                TotalPrice = totalPrice,
                OrderDate = DateTime.Now,
                Items = paymentDto.Items.Select(x => new InvoiceItemMessage
                {
                    ProductName = x.ProductName,
                    Price = x.Price
                }).ToList()
            };

            await _publishEndpoint.Publish(invoiceEvent);
            Console.WriteLine($"[PaymentsController] 📧 InvoiceRequested event published for OrderId: {paymentDto.OrderId}");
        }

        private async Task<Iyzipay.Model.Payment> ProcessIyzipayPayment(PaymentDto dto)
        {
            var request = new CreatePaymentRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = $"GameGaraj-{dto.OrderId}",
                Price = dto.TotalPrice.ToString("F2", CultureInfo.InvariantCulture),
                PaidPrice = dto.TotalPrice.ToString("F2", CultureInfo.InvariantCulture),
                Currency = Currency.TRY.ToString(),
                Installment = 1,
                BasketId = $"B{dto.OrderId}",
                PaymentChannel = PaymentChannel.WEB.ToString(),
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                PaymentCard = new PaymentCard
                {
                    CardHolderName = dto.CardName,
                    CardNumber = dto.CardNumber,
                    ExpireMonth = dto.ExpireMonth,
                    ExpireYear = dto.ExpireYear,
                    Cvc = dto.CVV,
                    RegisterCard = 0
                },
                Buyer = new Buyer
                {
                    Id = $"BY{dto.OrderId}",
                    Name = dto.CustomerName,
                    Surname = dto.CustomerSurname,
                    GsmNumber = dto.CustomerPhone,
                    Email = dto.CustomerEmail,
                    IdentityNumber = dto.CustomerIdentityNumber,
                    RegistrationAddress = dto.AddressDetail,
                    City = dto.City,
                    Country = dto.Country,
                    ZipCode = dto.ZipCode,
                    Ip = dto.CustomerIp
                },
                ShippingAddress = new Iyzipay.Model.Address
                {
                    ContactName = $"{dto.CustomerName} {dto.CustomerSurname}",
                    City = dto.City,
                    Country = dto.Country,
                    Description = dto.AddressDetail,
                    ZipCode = dto.ZipCode
                },
                BillingAddress = new Iyzipay.Model.Address
                {
                    ContactName = $"{dto.CustomerName} {dto.CustomerSurname}",
                    City = dto.City,
                    Country = dto.Country,
                    Description = dto.AddressDetail,
                    ZipCode = dto.ZipCode
                }
            };

            // Iyzico requires: PaidPrice == Sum(BasketItem.Price)
            // Since we have global discounts (coupons, threshold discounts), we must distribute them to items.
            var basketItems = new List<BasketItem>();
            decimal totalItemPrices = dto.Items.Sum(x => x.Price);
            decimal currentDistributedSum = 0;

            if (totalItemPrices > 0)
            {
                var items = dto.Items.ToList();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    decimal adjustedPrice;

                    if (i == items.Count - 1)
                    {
                        // Last item: Use remainder to avoid rounding issues
                        adjustedPrice = dto.TotalPrice - currentDistributedSum;
                    }
                    else
                    {
                        // Pro-rata distribution
                        adjustedPrice = Math.Round(item.Price * (dto.TotalPrice / totalItemPrices), 2);
                    }

                    basketItems.Add(new BasketItem
                    {
                        Id = $"BI{i + 1}",
                        Name = item.ProductName,
                        Category1 = "General",
                        ItemType = BasketItemType.PHYSICAL.ToString(),
                        Price = adjustedPrice.ToString("F2", CultureInfo.InvariantCulture)
                    });

                    currentDistributedSum += adjustedPrice;
                }
            }

            request.BasketItems = basketItems;

            Console.WriteLine($"[PaymentsController] Creating Iyzico payment request:");
            Console.WriteLine($"  - Price: {request.Price}");
            Console.WriteLine($"  - Buyer: {request.Buyer.Name} {request.Buyer.Surname}");
            Console.WriteLine($"  - Email: {request.Buyer.Email}");
            Console.WriteLine($"  - BasketItems Count: {request.BasketItems.Count}");
            foreach (var item in request.BasketItems)
            {
                Console.WriteLine($"    * {item.Name} - {item.Price} TL");
            }

            var result = await Task.Run(() => Iyzipay.Model.Payment.Create(request, _iyzipayOptions));

            Console.WriteLine($"[PaymentsController] Iyzico Response:");
            Console.WriteLine($"  - Status: {result.Status}");
            Console.WriteLine($"  - ErrorCode: {result.ErrorCode}");
            Console.WriteLine($"  - ErrorMessage: {result.ErrorMessage}");
            Console.WriteLine($"  - ErrorGroup: {result.ErrorGroup}");

            return result;
        }
    }
}
