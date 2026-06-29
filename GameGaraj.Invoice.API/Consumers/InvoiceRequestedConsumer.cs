using MassTransit;
using GameGaraj.Invoice.API.Models;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Shared.Events;
using GameGaraj.Shared.Observability.Metrics;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace GameGaraj.Invoice.API.Consumers
{
    /// <summary>
    /// InvoiceRequested event'ini dinler, fatura PDF'i oluşturup MinIO'ya atar,
    /// ardından SendNotification event'ini publish eder.
    /// </summary>
    public class InvoiceRequestedConsumer : IConsumer<InvoiceRequested>
    {
        private readonly IPdfGenerator _pdfGenerator;
        private readonly IStorageService _storageService;
        private readonly InvoiceMetrics _metrics;

        public InvoiceRequestedConsumer(
            IPdfGenerator pdfGenerator,
            IStorageService storageService,
            InvoiceMetrics metrics)
        {
            _pdfGenerator = pdfGenerator;
            _storageService = storageService;
            _metrics = metrics;
        }

        public async Task Consume(ConsumeContext<InvoiceRequested> context)
        {
            Console.WriteLine($"[InvoiceRequestedConsumer] Received InvoiceRequested for OrderId: {context.Message.OrderId}");
            Console.WriteLine($"[InvoiceRequestedConsumer] Generating PDF and uploading to MinIO storage...");

            _metrics.InvoiceGenerated();

            var invoiceData = new InvoiceData
            {
                OrderId = context.Message.OrderId,
                CustomerName = context.Message.CustomerName,
                CustomerEmail = context.Message.CustomerEmail,
                OrderDate = context.Message.OrderDate,
                TotalPrice = context.Message.TotalPrice,
                Items = context.Message.Items.Select(x => new InvoiceItem
                {
                    ProductName = x.ProductName,
                    Price = x.Price
                }).ToList()
            };

            try
            {
                byte[] pdfBytes;
                using (_metrics.TrackGeneration())
                {
                    pdfBytes = _pdfGenerator.GenerateInvoicePdf(invoiceData);
                }

                // Upload to MinIO Storage
                var fileName = $"INV-{invoiceData.OrderId:D6}.pdf";
                var relativePath = await _storageService.UploadFileAsync(pdfBytes, fileName, "application/pdf", context.CancellationToken);

                Console.WriteLine($"[InvoiceRequestedConsumer] ✅ PDF generated and uploaded to: {relativePath}");

                // Generate HTML mail content
                var emailBody = GenerateInvoiceEmailBody(invoiceData, relativePath);

                // Publish SendNotification event via MassTransit
                await context.Publish(new SendNotification
                {
                    Recipient = context.Message.CustomerEmail,
                    Type = "Email",
                    Title = $"GameGaraj - Sipariş Faturası #{invoiceData.OrderId}",
                    Body = emailBody,
                    AttachmentPath = relativePath,
                    AttachmentName = $"GameGaraj-Fatura-{invoiceData.InvoiceNumber}.pdf"
                });

                Console.WriteLine($"[InvoiceRequestedConsumer] 📧 SendNotification event published for OrderId: {invoiceData.OrderId}");
                _metrics.EmailSent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InvoiceRequestedConsumer] ❌ Invoice generation or notification publish failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                _metrics.EmailFailed();
                throw;
            }
        }

        private string FormatTurkishLira(decimal amount)
        {
            return amount.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
        }

        private string GenerateInvoiceEmailBody(InvoiceData invoice, string relativePath)
        {
            var itemsHtml = string.Join("", invoice.Items.Select(item => $@"
                <tr>
                    <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; color: #333;"">{item.ProductName}</td>
                    <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; color: #333;"">{FormatTurkishLira(item.Price)}</td>
                </tr>"));

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Sipariş Onayı</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f5f5f5;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff;"">
        
        <!-- Header -->
        <div style=""background-color: #1c1c1c; padding: 30px; text-align: center;"">
            <h1 style=""color: #0d6efd; margin: 0; font-size: 28px;"">GameGaraj</h1>
        </div>
        
        <!-- Thank You Section -->
        <div style=""padding: 40px; text-align: center;"">
            <h1 style=""color: #1c1c1c; margin: 0 0 10px 0; font-size: 32px;"">Teşekkürler!</h1>
            <p style=""color: #666; margin: 0; font-size: 16px;"">Merhaba {invoice.CustomerName}!</p>
            <p style=""color: #666; margin: 5px 0 0 0; font-size: 16px;"">Satın alımınız için teşekkür ederiz!</p>
            <p style=""color: #0d6efd; margin: 15px 0 0 0; font-size: 16px; font-weight: bold;"">Faturanız e-posta ekinde yer almaktadır.</p>
        </div>
        
        <!-- Invoice ID -->
        <div style=""padding: 20px 40px; text-align: center;"">
            <p style=""color: #666; margin: 0; font-size: 14px; font-weight: bold;"">FATURA KİMLİĞİ:</p>
            <h2 style=""color: #1c1c1c; margin: 10px 0; font-size: 28px; font-weight: bold;"">{invoice.InvoiceNumber}</h2>
        </div>
        
        <!-- Order Info -->
        <div style=""padding: 20px 40px; border-top: 1px solid #e0e0e0;"">
            <p style=""color: #666; margin: 0 0 15px 0; font-size: 12px; font-weight: bold; text-transform: uppercase;"">SİPARİŞ BİLGİLERİ:</p>
            
            <table style=""width: 100%; font-size: 14px;"">
                <tr>
                    <td style=""color: #666; padding: 5px 0;"">Sipariş ID:</td>
                    <td style=""color: #333; padding: 5px 0;"">{invoice.OrderId}</td>
                </tr>
                <tr>
                    <td style=""color: #666; padding: 5px 0;"">Tarih:</td>
                    <td style=""color: #333; padding: 5px 0;"">{invoice.OrderDate:dd MMMM yyyy HH:mm}</td>
                </tr>
                <tr>
                    <td style=""color: #666; padding: 5px 0;"">Email:</td>
                    <td style=""color: #333; padding: 5px 0;"">{invoice.CustomerEmail}</td>
                </tr>
            </table>
        </div>
        
        <!-- Items -->
        <div style=""padding: 20px 40px;"">
            <p style=""color: #666; margin: 0 0 15px 0; font-size: 12px; font-weight: bold; text-transform: uppercase;"">SATIN ALINAN ÜRÜNLER:</p>
            
            <table style=""width: 100%; border-collapse: collapse;"">
                <thead>
                    <tr style=""background-color: #f9f9f9;"">
                        <th style=""padding: 12px; text-align: left; color: #666; font-weight: 600; font-size: 12px; text-transform: uppercase;"">Ürün</th>
                        <th style=""padding: 12px; text-align: right; color: #666; font-weight: 600; font-size: 12px; text-transform: uppercase;"">Fiyat</th>
                    </tr>
                </thead>
                <tbody>
                    {itemsHtml}
                </tbody>
                <tfoot>
                    <tr style=""background-color: #f9f9f9;"">
                        <td style=""padding: 15px 12px; font-weight: bold; color: #1c1c1c;"">TOPLAM</td>
                        <td style=""padding: 15px 12px; text-align: right; font-weight: bold; color: #0d6efd; font-size: 18px;"">{FormatTurkishLira(invoice.TotalPrice)}</td>
                    </tr>
                </tfoot>
            </table>
        </div>
        
        <!-- Footer -->
        <div style=""background-color: #1c1c1c; padding: 30px; text-align: center;"">
            <p style=""color: #999; margin: 0; font-size: 12px;"">Bu email otomatik olarak gönderilmiştir.</p>
            <p style=""color: #999; margin: 5px 0 0 0; font-size: 12px;"">© 2026 GameGaraj - Tüm hakları saklıdır.</p>
        </div>
        
    </div>
</body>
</html>";
        }
    }
}
