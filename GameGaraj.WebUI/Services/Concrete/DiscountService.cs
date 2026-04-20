using GameGaraj.WebUI.Models.Discounts;
using GameGaraj.WebUI.Services.Abstract;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class DiscountService : IDiscountService
    {
        private readonly HttpClient _httpClient;

        public DiscountService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<DiscountViewModel>> GetAllAsync()
        {
            var response = await _httpClient.GetAsync("discounts");
            if (!response.IsSuccessStatusCode)
                return new List<DiscountViewModel>();

            var discounts = await response.Content.ReadFromJsonAsync<List<DiscountViewModel>>();
            return discounts ?? new List<DiscountViewModel>();
        }

        public async Task<DiscountViewModel?> GetByIdAsync(int id)
        {
            var response = await _httpClient.GetAsync($"discounts/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DiscountViewModel>();
        }

        public async Task<bool> SaveAsync(CreateDiscountInput input)
        {
            var response = await _httpClient.PostAsJsonAsync("discounts", input);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateAsync(UpdateDiscountInput input)
        {
            var response = await _httpClient.PutAsJsonAsync("discounts", input);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"discounts/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<DiscountViewModel?> GetByCodeAsync(string code)
        {
            var response = await _httpClient.GetAsync($"discounts/code/{code}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DiscountViewModel>();
        }
    }
}
