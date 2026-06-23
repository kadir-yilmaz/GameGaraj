using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class PhotoStockService : IPhotoStockService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PhotoStockService> _logger;

        public PhotoStockService(HttpClient httpClient, ILogger<PhotoStockService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<string>> UploadPhotosAsync(IFormFileCollection photos, string? brand, string? productName)
        {
            if (photos == null || photos.Count == 0)
                return new List<string>();

            using var multipartFormDataContent = new MultipartFormDataContent();
            multipartFormDataContent.Add(new StringContent(brand ?? string.Empty), "brand");
            multipartFormDataContent.Add(new StringContent(productName ?? string.Empty), "productName");

            foreach (var photo in photos)
            {
                var streamContent = new StreamContent(photo.OpenReadStream());
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType);
                multipartFormDataContent.Add(streamContent, "photos", photo.FileName);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("photos", multipartFormDataContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PhotoStock API request failed while uploading photos");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "PhotoStock API returned {StatusCode} while uploading photos. Body: {Body}",
                    (int)response.StatusCode,
                    errorBody);
                return new List<string>();
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseString);

            if (jsonDocument.RootElement.TryGetProperty("urls", out var urlsElement))
            {
                var urls = urlsElement.Deserialize<List<string>>();
                return urls ?? new List<string>();
            }

            return new List<string>();
        }

        public async Task<bool> DeletePhotoAsync(string photoUrl)
        {
            // Remove full path/domain if present, keep only the filename
            var fileName = Path.GetFileName(photoUrl);
            var response = await _httpClient.DeleteAsync($"photos/{fileName}");
            return response.IsSuccessStatusCode;
        }
    }
}
