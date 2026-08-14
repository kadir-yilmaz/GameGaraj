package service

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"time"

	"go.uber.org/zap"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/cache"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/model"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/repository"
)

// SearchService is the thin business logic layer between handlers and repository.
// It implements the cache-aside pattern for all read operations.
type SearchService struct {
	repo   *repository.ElasticRepo
	cache  *cache.Service
	logger *zap.Logger
}

// NewSearchService creates a new search service
func NewSearchService(repo *repository.ElasticRepo, cache *cache.Service, logger *zap.Logger) *SearchService {
	return &SearchService{
		repo:   repo,
		cache:  cache,
		logger: logger,
	}
}

// -------------------------------------------------------------------
// Search Operations (with cache)
// -------------------------------------------------------------------

// Search performs a cached full-text product search
func (s *SearchService) Search(ctx context.Context, keyword string) ([]model.ProductDTO, error) {
	if keyword == "" {
		return []model.ProductDTO{}, nil
	}

	cacheKey := "query_" + normalizeForCacheKey(keyword)

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result []model.ProductDTO
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			s.logger.Debug("Search cache HIT", zap.String("keyword", keyword))
			return result, nil
		}
	}

	// 2. Query Elasticsearch
	docs, err := s.repo.Search(ctx, keyword)
	if err != nil {
		return nil, err
	}

	result := repository.DocumentsToDTOs(docs)

	// 3. Write to cache (TTL 2 minutes)
	if len(result) > 0 {
		if data, err := json.Marshal(result); err == nil {
			s.cache.Set(ctx, cacheKey, string(data), 2*time.Minute)
		}
	}

	return result, nil
}

// Suggestions returns cached autocomplete suggestions
func (s *SearchService) Suggestions(ctx context.Context, keyword string) ([]model.SearchSuggestion, error) {
	if keyword == "" {
		return []model.SearchSuggestion{}, nil
	}

	cacheKey := "suggestions_" + normalizeForCacheKey(keyword)

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result []model.SearchSuggestion
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			return result, nil
		}
	}

	// 2. Query Elasticsearch
	docs, err := s.repo.Suggestions(ctx, keyword)
	if err != nil {
		return nil, err
	}

	// 3. Build suggestions (product, brand, category)
	suggestions := buildSuggestions(docs)

	// 4. Write to cache (TTL 2 minutes)
	if len(suggestions) > 0 {
		if data, err := json.Marshal(suggestions); err == nil {
			s.cache.Set(ctx, cacheKey, string(data), 2*time.Minute)
		}
	}

	return suggestions, nil
}

// Facets returns cached search facets (brand/category aggregations)
func (s *SearchService) Facets(ctx context.Context, keyword string) (*model.SearchFacetResult, error) {
	cacheKey := "facets_" + normalizeForCacheKey(keyword)
	if keyword == "" {
		cacheKey = "facets_all"
	}

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result model.SearchFacetResult
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			return &result, nil
		}
	}

	// 2. Query Elasticsearch
	result, err := s.repo.Facets(ctx, keyword)
	if err != nil {
		return nil, err
	}

	// 3. Write to cache (TTL 5 minutes)
	if data, err := json.Marshal(result); err == nil {
		s.cache.Set(ctx, cacheKey, string(data), 5*time.Minute)
	}

	return result, nil
}

// Brands returns brand suggestions by keyword (no cache)
func (s *SearchService) Brands(ctx context.Context, keyword string) ([]string, error) {
	if keyword == "" {
		return []string{}, nil
	}
	return s.repo.GetBrandsByKeyword(ctx, keyword)
}

// -------------------------------------------------------------------
// Product Read Operations (with cache)
// -------------------------------------------------------------------

// GetFeatured returns cached featured products
func (s *SearchService) GetFeatured(ctx context.Context) ([]model.ProductDTO, error) {
	cacheKey := "featured_products"

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result []model.ProductDTO
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			return result, nil
		}
	}

	// 2. Query Elasticsearch
	docs, err := s.repo.GetFeatured(ctx)
	if err != nil {
		return nil, err
	}

	result := repository.DocumentsToDTOs(docs)

	// 3. Write to cache (TTL 10 minutes)
	if len(result) > 0 {
		if data, err := json.Marshal(result); err == nil {
			s.cache.Set(ctx, cacheKey, string(data), 10*time.Minute)
		}
	}

	return result, nil
}

// GetByID returns a cached product by ID
func (s *SearchService) GetByID(ctx context.Context, id string) (*model.ProductDTO, error) {
	cacheKey := "product_" + id

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result model.ProductDTO
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			return &result, nil
		}
	}

	// 2. Query Elasticsearch
	doc, err := s.repo.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}
	if doc == nil {
		return nil, nil
	}

	dto := repository.DocumentToDTO(*doc)

	// 3. Write to cache (TTL 10 minutes)
	if data, err := json.Marshal(dto); err == nil {
		s.cache.Set(ctx, cacheKey, string(data), 10*time.Minute)
	}

	return &dto, nil
}

// GetBySlug returns a cached product by slug
func (s *SearchService) GetBySlug(ctx context.Context, slug string) (*model.ProductDTO, error) {
	cacheKey := "product_slug_" + slug

	// 1. Check cache
	if cached, ok := s.cache.Get(ctx, cacheKey); ok {
		var result model.ProductDTO
		if err := json.Unmarshal([]byte(cached), &result); err == nil {
			return &result, nil
		}
	}

	// 2. Query Elasticsearch
	doc, err := s.repo.GetBySlug(ctx, slug)
	if err != nil {
		return nil, err
	}
	if doc == nil {
		return nil, nil
	}

	dto := repository.DocumentToDTO(*doc)

	// 3. Write to cache (TTL 10 minutes)
	if data, err := json.Marshal(dto); err == nil {
		s.cache.Set(ctx, cacheKey, string(data), 10*time.Minute)
	}

	return &dto, nil
}

// GetFiltered returns filtered and sorted product listings (no cache — too many combinations)
func (s *SearchService) GetFiltered(
	ctx context.Context,
	categoryID string,
	categoryIDs []string,
	sortBy string,
	minPrice, maxPrice *float64,
	brand string,
	specs map[string]string,
) ([]model.ProductDTO, error) {
	docs, err := s.repo.GetAllActive(ctx)
	if err != nil {
		return nil, err
	}

	filtered := repository.FilterProducts(docs, categoryID, categoryIDs, minPrice, maxPrice, brand, specs, sortBy)
	return repository.DocumentsToDTOs(filtered), nil
}

// -------------------------------------------------------------------
// Index Operations
// -------------------------------------------------------------------

// GetStatus returns the index status
func (s *SearchService) GetStatus(ctx context.Context) *model.SearchIndexStatus {
	return s.repo.GetStatus(ctx)
}

// GetDocumentPreviews returns paginated document previews
func (s *SearchService) GetDocumentPreviews(ctx context.Context, page, pageSize int) (*model.PagedResult, error) {
	docs, totalCount, err := s.repo.GetDocumentPreviews(ctx, page, pageSize)
	if err != nil {
		return nil, err
	}

	previews := make([]model.SearchIndexDocumentPreview, 0, len(docs))
	for _, doc := range docs {
		previews = append(previews, repository.DocumentToPreview(doc))
	}

	return &model.PagedResult{
		Items:      previews,
		Page:       page,
		PageSize:   pageSize,
		TotalCount: totalCount,
		TotalPages: repository.TotalPages(totalCount, pageSize),
	}, nil
}

// -------------------------------------------------------------------
// Cache Invalidation (called by RabbitMQ consumer)
// -------------------------------------------------------------------

// InvalidateProductCache clears all cache entries related to a product change
func (s *SearchService) InvalidateProductCache(ctx context.Context, productID, slug string) {
	keys := []string{"featured_products"}
	if productID != "" {
		keys = append(keys, "product_"+productID)
	}
	if slug != "" {
		keys = append(keys, "product_slug_"+slug)
	}
	s.cache.Delete(ctx, keys...)

	// Invalidate all search and suggestion caches since product data changed
	s.cache.DeletePattern(ctx, "query_*")
	s.cache.DeletePattern(ctx, "suggestions_*")
	s.cache.DeletePattern(ctx, "facets_*")

	s.logger.Info("Product cache invalidated",
		zap.String("productId", productID),
		zap.String("slug", slug))
}

// InvalidateAllCache clears all search cache (used during reindex)
func (s *SearchService) InvalidateAllCache(ctx context.Context) {
	s.cache.DeletePattern(ctx, "*")
	s.logger.Info("All search cache invalidated")
}

// ReindexFromCatalog fetches all products from Catalog API, bulk indexes them into Elasticsearch, and clears Redis caches.
func (s *SearchService) ReindexFromCatalog(ctx context.Context, catalogURL string) (*model.ReindexResult, error) {
	catalogURL = strings.TrimRight(catalogURL, "/")
	productsURL := fmt.Sprintf("%s/api/products", catalogURL)

	s.logger.Info("Starting full reindex from Catalog API", zap.String("url", productsURL))

	client := &http.Client{Timeout: 30 * time.Second}
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, productsURL, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}

	resp, err := client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("failed to fetch products from catalog: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode >= 400 {
		return nil, fmt.Errorf("catalog API returned status %d", resp.StatusCode)
	}

	var products []struct {
		ID            string            `json:"id"`
		Name          string            `json:"name"`
		Brand         string            `json:"brand"`
		Slug          string            `json:"slug"`
		Description   string            `json:"description"`
		Price         float64           `json:"price"`
		Stock         int               `json:"stock"`
		ReservedStock int               `json:"reservedStock"`
		IsActive      bool              `json:"isActive"`
		IsFeatured    bool              `json:"isFeatured"`
		ImageUrls     []string          `json:"imageUrls"`
		CreatedAt     time.Time         `json:"createdAt"`
		CategoryID    string            `json:"categoryId"`
		CategoryName  string            `json:"categoryName"`
		Specs         map[string]string `json:"specs"`
	}

	if err := json.NewDecoder(resp.Body).Decode(&products); err != nil {
		return nil, fmt.Errorf("failed to decode products: %w", err)
	}

	total := len(products)
	if total == 0 {
		s.logger.Warn("No products found in Catalog API to reindex")
		return &model.ReindexResult{Total: 0, Succeeded: 0, Failed: 0, Errors: []string{}}, nil
	}

	// Recreate ES index for a clean slate
	if err := s.repo.EnsureIndex(ctx, true); err != nil {
		s.logger.Warn("Failed to recreate index during reindex", zap.Error(err))
	}

	docs := make([]model.ProductSearchDocument, 0, total)
	now := time.Now().UTC()

	for _, p := range products {
		event := model.ProductEvent{
			ID:            p.ID,
			Name:          p.Name,
			Brand:         p.Brand,
			Slug:          p.Slug,
			Description:   p.Description,
			Price:         p.Price,
			Stock:         p.Stock,
			ReservedStock: p.ReservedStock,
			IsActive:      p.IsActive,
			IsFeatured:    p.IsFeatured,
			ImageUrls:     p.ImageUrls,
			CategoryID:    p.CategoryID,
			CategoryName:  p.CategoryName,
			Specs:         p.Specs,
		}
		doc := event.ToSearchDocument()
		doc.IndexedAt = &now
		docs = append(docs, *doc)
	}

	succeeded, bulkErr := s.repo.BulkIndex(ctx, docs)
	var errors []string
	if bulkErr != nil {
		errors = append(errors, bulkErr.Error())
	}

	// Clear all search caches in Redis
	s.InvalidateAllCache(ctx)

	result := &model.ReindexResult{
		Total:     total,
		Succeeded: succeeded,
		Failed:    total - succeeded,
		Errors:    errors,
	}

	s.logger.Info("Full reindex completed",
		zap.Int("total", result.Total),
		zap.Int("succeeded", result.Succeeded),
		zap.Int("failed", result.Failed))

	return result, nil
}

// IndexProduct indexes a product and invalidates relevant caches
func (s *SearchService) IndexProduct(ctx context.Context, event *model.ProductEvent) error {
	doc := event.ToSearchDocument()
	if err := s.repo.IndexProduct(ctx, doc); err != nil {
		return err
	}
	s.InvalidateProductCache(ctx, event.ID, event.Slug)
	return nil
}

// DeleteProduct removes a product and invalidates relevant caches
func (s *SearchService) DeleteProduct(ctx context.Context, productID string) error {
	if err := s.repo.DeleteProduct(ctx, productID); err != nil {
		return err
	}
	s.InvalidateProductCache(ctx, productID, "")
	return nil
}

// -------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------

func normalizeForCacheKey(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}

func buildSuggestions(docs []model.ProductSearchDocument) []model.SearchSuggestion {
	suggestions := make([]model.SearchSuggestion, 0)

	// Product suggestions (max 10)
	limit := 10
	if len(docs) < limit {
		limit = len(docs)
	}
	for i := 0; i < limit; i++ {
		p := docs[i]
		suggestion := model.SearchSuggestion{
			Type:  "product",
			ID:    p.ID,
			Name:  p.Name,
			Slug:  &p.Slug,
			Price: &p.Price,
		}
		if len(p.ImageUrls) > 0 {
			suggestion.ImageURL = &p.ImageUrls[0]
		}
		suggestions = append(suggestions, suggestion)
	}

	// Brand suggestions (max 5, unique)
	brandSeen := make(map[string]bool)
	brandCount := 0
	for _, p := range docs {
		brand := strings.TrimSpace(p.Brand)
		if brand == "" || brandSeen[strings.ToLower(brand)] || brandCount >= 5 {
			continue
		}
		brandSeen[strings.ToLower(brand)] = true
		brandCount++
		suggestions = append(suggestions, model.SearchSuggestion{
			Type: "brand",
			ID:   brand,
			Name: brand,
		})
	}

	// Category suggestions (max 5, unique)
	catSeen := make(map[string]bool)
	catCount := 0
	for _, p := range docs {
		if p.CategoryName == "" || catSeen[p.CategoryID] || catCount >= 5 {
			continue
		}
		catSeen[p.CategoryID] = true
		catCount++
		slug := p.CategorySlug
		suggestions = append(suggestions, model.SearchSuggestion{
			Type: "category",
			ID:   p.CategoryID,
			Name: p.CategoryName,
			Slug: &slug,
		})
	}

	return suggestions
}
