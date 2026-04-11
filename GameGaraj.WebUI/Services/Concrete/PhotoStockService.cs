using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class PhotoStockService : IPhotoStockService
    {
        private readonly HttpClient _httpClient;

        public PhotoStockService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<string>> UploadPhotosAsync(IFormFileCollection photos)
        {
            if (photos == null || photos.Count == 0)
                return new List<string>();

            var multipartFormDataContent = new MultipartFormDataContent();

            foreach (var photo in photos)
            {
                var streamContent = new StreamContent(photo.OpenReadStream());
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType);
                multipartFormDataContent.Add(streamContent, "photos", photo.FileName);
            }

            var response = await _httpClient.PostAsync("api/photos", multipartFormDataContent);

            if (!response.IsSuccessStatusCode)
                return new List<string>();

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
            var response = await _httpClient.DeleteAsync($"api/photos/{fileName}");
            return response.IsSuccessStatusCode;
        }
    }
}
