namespace GameGaraj.WebUI.Models.Products
{
    public class SearchIndexStatusViewModel
    {
        public bool IsConnected { get; set; }
        public string Status { get; set; } = "Offline";
        public long IndexedProductCount { get; set; }
        public DateTime? LastIndexedAt { get; set; }
        public int PendingIndexQueueCount { get; set; }
        public int FailedIndexingCount { get; set; }
        public DateTime? LastFailedIndexingAt { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class ReindexResultViewModel
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class SearchIndexDocumentPreviewViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CategorySlug { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int ReservedStock { get; set; }
        public int AvailableStock { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public bool InStock { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public Dictionary<string, string> Specs { get; set; } = new();
        public List<string> SpecValues { get; set; } = new();
        public string SearchText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastIndexedAt { get; set; }
    }

    public class SearchIndexDocumentPageViewModel
    {
        public List<SearchIndexDocumentPreviewViewModel> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
