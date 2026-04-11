using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using GameGaraj.Invoice.API.Models;
using GameGaraj.Invoice.API.Settings;

namespace GameGaraj.Invoice.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendInvoiceEmailAsync(InvoiceData invoice);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task<bool> SendInvoiceEmailAsync(InvoiceData invoice)
        {
            try
            {
                Console.WriteLine($"[EmailService] Attempting to send email...");
                Console.WriteLine($"  - SMTP Host: {_emailSettings.SmtpHost}");
                Console.WriteLine($"  - SMTP Port: {_emailSettings.SmtpPort}");
                Console.WriteLine($"  - SMTP Username: {_emailSettings.SmtpUsername}");
                Console.WriteLine($"  - Enable SSL: {_emailSettings.EnableSsl}");
                Console.WriteLine($"  - From: {_emailSettings.FromEmail}");
                Console.WriteLine($"  - To: kadiryilmaz.dev@gmail.com (TEST MODE)");
                
                var htmlBody = GenerateReceiptHtml(invoice);

                using var smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword),
                    EnableSsl = _emailSettings.EnableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = $"GameGaraj - Sipariş Onayı #{invoice.OrderId}",
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                
                mailMessage.To.Add("kadiryilmaz.dev@gmail.com");

                Console.WriteLine($"[EmailService] Sending email via SMTP...");
                await smtpClient.SendMailAsync(mailMessage);
                
                Console.WriteLine($"📧 Email sent successfully to: {invoice.CustomerEmail}");
                return true;
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"❌ SMTP Error: {smtpEx.Message}");
                Console.WriteLine($"   Status Code: {smtpEx.StatusCode}");
                Console.WriteLine($"   Inner Exception: {smtpEx.InnerException?.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Email sending failed: {ex.Message}");
                Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        private string GenerateReceiptHtml(InvoiceData invoice)
        {
            var itemsHtml = string.Join("", invoice.Items.Select(item => $@"
                <tr>
                    <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; color: #333;"">{item.ProductName}</td>
                    <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; color: #333;"">{item.Price:C}</td>
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
        </div>
        
        <!-- Invoice ID -->
        <div style=""padding: 20px 40px; text-align: center;"">
            <p style=""color: #666; margin: 0; font-size: 14px; font-weight: bold;"">FATURA KİMLİĞİ:</p>
            <h2 style=""color: #1c1c1c; margin: 10px 0; font-size: 28px; font-weight: bold;"">INV-{invoice.InvoiceNumber}</h2>
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
                        <td style=""padding: 15px 12px; text-align: right; font-weight: bold; color: #0d6efd; font-size: 18px;"">{invoice.TotalPrice:C}</td>
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
