package model

// APIResponse is the standard wrapper for all API responses
type APIResponse struct {
	Data    interface{} `json:"data,omitempty"`
	Message string      `json:"message,omitempty"`
	Error   string      `json:"error,omitempty"`
}

// ProductDTO is the public-facing product representation returned by search endpoints.
// Mirrors the C# ProductDto in Catalog.API/Dtos/ProductDtos.cs
type ProductDTO struct {
	ID             string            `json:"id"`
	Name           string            `json:"name"`
	Brand          string            `json:"brand"`
	Slug           string            `json:"slug"`
	Description    string            `json:"description"`
	Price          float64           `json:"price"`
	Stock          int               `json:"stock"`
	ReservedStock  int               `json:"reservedStock"`
	AvailableStock int               `json:"availableStock"`
	IsActive       bool              `json:"isActive"`
	IsFeatured     bool              `json:"isFeatured"`
	ImageUrls      []string          `json:"imageUrls"`
	CreatedAt      string            `json:"createdAt"`
	CategoryID     string            `json:"categoryId"`
	CategoryName   string            `json:"categoryName,omitempty"`
	Specs          map[string]string `json:"specs"`
}

// PagedResult is a generic paginated response
type PagedResult struct {
	Items      interface{} `json:"items"`
	Page       int         `json:"page"`
	PageSize   int         `json:"pageSize"`
	TotalCount int64       `json:"totalCount"`
	TotalPages int         `json:"totalPages"`
}

// SearchSuggestion mirrors the C# SearchSuggestionDto
type SearchSuggestion struct {
	Type     string   `json:"type"`
	ID       string   `json:"id"`
	Name     string   `json:"name"`
	Slug     *string  `json:"slug,omitempty"`
	ImageURL *string  `json:"imageUrl,omitempty"`
	Price    *float64 `json:"price,omitempty"`
}

// SearchFacetResult mirrors the C# SearchFacetResultDto
type SearchFacetResult struct {
	Brands     []SearchFacetItem `json:"brands"`
	Categories []SearchFacetItem `json:"categories"`
}

// SearchFacetItem mirrors the C# SearchFacetItemDto
type SearchFacetItem struct {
	Value string `json:"value"`
	Count int64  `json:"count"`
}

// ReindexResult mirrors the C# ReindexResultDto
type ReindexResult struct {
	Total     int      `json:"total"`
	Succeeded int      `json:"succeeded"`
	Failed    int      `json:"failed"`
	Errors    []string `json:"errors"`
}

// SearchIndexStatus mirrors the C# SearchIndexStatusDto
type SearchIndexStatus struct {
	IsConnected           bool    `json:"isConnected"`
	Status                string  `json:"status"`
	IndexedProductCount   int64   `json:"indexedProductCount"`
	LastIndexedAt         *string `json:"lastIndexedAt,omitempty"`
	PendingIndexQueueCount int    `json:"pendingIndexQueueCount"`
	FailedIndexingCount   int     `json:"failedIndexingCount"`
	ErrorMessage          *string `json:"errorMessage,omitempty"`
	CheckedAt             string  `json:"checkedAt"`
}

// SearchIndexDocumentPreview mirrors the C# SearchIndexDocumentPreviewDto
type SearchIndexDocumentPreview struct {
	ID             string            `json:"id"`
	Name           string            `json:"name"`
	Brand          string            `json:"brand"`
	Slug           string            `json:"slug"`
	CategoryID     string            `json:"categoryId"`
	CategoryName   string            `json:"categoryName"`
	CategorySlug   string            `json:"categorySlug"`
	Price          float64           `json:"price"`
	Stock          int               `json:"stock"`
	ReservedStock  int               `json:"reservedStock"`
	AvailableStock int               `json:"availableStock"`
	IsActive       bool              `json:"isActive"`
	IsFeatured     bool              `json:"isFeatured"`
	InStock        bool              `json:"inStock"`
	ImageUrls      []string          `json:"imageUrls"`
	Specs          map[string]string `json:"specs"`
	SpecValues     []string          `json:"specValues"`
	SearchText     string            `json:"searchText"`
	CreatedAt      string            `json:"createdAt"`
	LastIndexedAt  *string           `json:"lastIndexedAt,omitempty"`
}
