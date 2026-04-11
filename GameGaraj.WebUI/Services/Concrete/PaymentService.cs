using GameGaraj.WebUI.Models.Orders;
using GameGaraj.WebUI.Services.Abstract;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class PaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(HttpClient httpClient, ILogger<PaymentService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPayment(PaymentRequest paymentRequest)
        {
            try
            {
                var json = JsonSerializer.Serialize(paymentRequest, new JsonSerializerOptions { WriteIndented = true });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"[PaymentService] Processing payment for OrderId: {paymentRequest.OrderId}");
                _logger.LogInformation($"[PaymentService] Request URL: {_httpClient.BaseAddress}");
                _logger.LogInformation($"[PaymentService] Request Body: {json}");
                
                var response = await _httpClient.PostAsync("payments", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"[PaymentService] Response Status: {response.StatusCode}");
                _logger.LogInformation($"[PaymentService] Response Body: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"[PaymentService] Payment failed with status {response.StatusCode}");
                    
                    // Try to parse error from response
                    try
                    {
                        var errorResult = JsonSerializer.Deserialize<PaymentResult>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (errorResult != null && !string.IsNullOrEmpty(errorResult.Message))
                        {
                            return errorResult;
                        }
                    }
                    catch { }
                    
                    return new PaymentResult 
                    { 
                        Success = false, 
                        Message = $"Ödeme işlemi başarısız: {responseContent}" 
                    };
                }

                var result = JsonSerializer.Deserialize<PaymentResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new PaymentResult { Success = false, Message = "Ödeme yanıtı alınamadı" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PaymentService] Error processing payment");
                return new PaymentResult 
                { 
                    Success = false, 
                    Message = $"Bir hata oluştu: {ex.Message}" 
                };
            }
        }
    }
}
