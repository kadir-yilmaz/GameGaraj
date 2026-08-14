package repository

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"math"
	"net/http"
	"sort"
	"strings"
	"time"

	"github.com/elastic/go-elasticsearch/v8"
	"go.uber.org/zap"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/model"
)

const indexName = "products"

// ElasticRepo provides Elasticsearch operations for product search documents
type ElasticRepo struct {
	client    *elasticsearch.Client
	esURI     string
	logger    *zap.Logger
}

// NewElasticRepo creates a new Elasticsearch repository
func NewElasticRepo(esURI string, logger *zap.Logger) (*ElasticRepo, error) {
	cfg := elasticsearch.Config{
		Addresses: []string{esURI},
	}
	client, err := elasticsearch.NewClient(cfg)
	if err != nil {
		return nil, fmt.Errorf("failed to create elasticsearch client: %w", err)
	}

	return &ElasticRepo{
		client: client,
		esURI:  strings.TrimRight(esURI, "/"),
		logger: logger,
	}, nil
}

// Ping checks if Elasticsearch is reachable
func (r *ElasticRepo) Ping(ctx context.Context) error {
	res, err := r.client.Ping(r.client.Ping.WithContext(ctx))
	if err != nil {
		return err
	}
	defer res.Body.Close()
	if res.IsError() {
		return fmt.Errorf("elasticsearch ping failed: %s", res.Status())
	}
	return nil
}

// -------------------------------------------------------------------
// Search Operations
// -------------------------------------------------------------------

// Search performs a full-text search across multiple product fields
// Mirrors the C# SearchAsync logic in ProductQueryService.cs
func (r *ElasticRepo) Search(ctx context.Context, keyword string) ([]model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 20,
		"query": map[string]interface{}{
			"bool": map[string]interface{}{
				"must": []interface{}{
					map[string]interface{}{
						"multi_match": map[string]interface{}{
							"fields": []string{
								"name^5",
								"brand^4",
								"categoryName^3",
								"specValues^2",
								"searchText",
								"description",
							},
							"query":                keyword,
							"fuzziness":             "AUTO",
							"minimum_should_match":   "70%",
							"prefix_length":          1,
						},
					},
				},
				"filter": []interface{}{
					map[string]interface{}{
						"term": map[string]interface{}{
							"isActive": true,
						},
					},
				},
				"should": []interface{}{
					map[string]interface{}{
						"term": map[string]interface{}{
							"isFeatured": map[string]interface{}{
								"value": true,
								"boost": 2,
							},
						},
					},
					map[string]interface{}{
						"term": map[string]interface{}{
							"inStock": map[string]interface{}{
								"value": true,
								"boost": 1.5,
							},
						},
					},
				},
			},
		},
	}

	return r.executeSearch(ctx, query)
}

// Suggestions performs an autocomplete-style search with lower match threshold
// Mirrors the C# GetSuggestionsAsync logic
func (r *ElasticRepo) Suggestions(ctx context.Context, keyword string) ([]model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 20,
		"query": map[string]interface{}{
			"bool": map[string]interface{}{
				"must": []interface{}{
					map[string]interface{}{
						"multi_match": map[string]interface{}{
							"fields": []string{
								"name^5",
								"brand^4",
								"categoryName^3",
								"specValues^2",
								"searchText",
							},
							"query":                keyword,
							"fuzziness":             "AUTO",
							"minimum_should_match":   "60%",
							"prefix_length":          1,
						},
					},
				},
				"filter": []interface{}{
					map[string]interface{}{
						"term": map[string]interface{}{
							"isActive": true,
						},
					},
				},
			},
		},
	}

	return r.executeSearch(ctx, query)
}

// Facets returns brand and category aggregations, optionally filtered by keyword
// Mirrors the C# GetSearchFacetsAsync logic
func (r *ElasticRepo) Facets(ctx context.Context, keyword string) (*model.SearchFacetResult, error) {
	boolQuery := map[string]interface{}{
		"filter": []interface{}{
			map[string]interface{}{
				"term": map[string]interface{}{"isActive": true},
			},
		},
	}

	if keyword != "" {
		boolQuery["must"] = []interface{}{
			map[string]interface{}{
				"multi_match": map[string]interface{}{
					"fields": []string{
						"name^5",
						"brand^4",
						"categoryName^3",
						"specValues^2",
						"searchText",
					},
					"query":                keyword,
					"fuzziness":             "AUTO",
					"minimum_should_match":   "60%",
					"prefix_length":          1,
				},
			},
		}
	}

	query := map[string]interface{}{
		"size":  0,
		"query": map[string]interface{}{"bool": boolQuery},
		"aggs": map[string]interface{}{
			"brands": map[string]interface{}{
				"terms": map[string]interface{}{
					"field": "brand.keyword",
					"size":  20,
				},
			},
			"categories": map[string]interface{}{
				"terms": map[string]interface{}{
					"field": "categoryName.keyword",
					"size":  20,
				},
			},
		},
	}

	body, err := json.Marshal(query)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal facets query: %w", err)
	}

	res, err := r.client.Search(
		r.client.Search.WithContext(ctx),
		r.client.Search.WithIndex(indexName),
		r.client.Search.WithBody(bytes.NewReader(body)),
	)
	if err != nil {
		return nil, fmt.Errorf("facets search failed: %w", err)
	}
	defer res.Body.Close()

	if res.IsError() {
		return nil, fmt.Errorf("facets search error: %s", res.Status())
	}

	var result struct {
		Aggregations struct {
			Brands     aggResult `json:"brands"`
			Categories aggResult `json:"categories"`
		} `json:"aggregations"`
	}
	if err := json.NewDecoder(res.Body).Decode(&result); err != nil {
		return nil, fmt.Errorf("failed to decode facets response: %w", err)
	}

	facets := &model.SearchFacetResult{
		Brands:     parseBuckets(result.Aggregations.Brands),
		Categories: parseBuckets(result.Aggregations.Categories),
	}

	return facets, nil
}

type aggResult struct {
	Buckets []struct {
		Key      string `json:"key"`
		DocCount int64  `json:"doc_count"`
	} `json:"buckets"`
}

func parseBuckets(agg aggResult) []model.SearchFacetItem {
	items := make([]model.SearchFacetItem, 0, len(agg.Buckets))
	for _, b := range agg.Buckets {
		if b.Key != "" {
			items = append(items, model.SearchFacetItem{
				Value: b.Key,
				Count: b.DocCount,
			})
		}
	}
	return items
}

// GetAllActive retrieves all active products from Elasticsearch
// Mirrors the C# GetAllFromElasticAsync logic
func (r *ElasticRepo) GetAllActive(ctx context.Context) ([]model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 1000,
		"query": map[string]interface{}{
			"bool": map[string]interface{}{
				"filter": []interface{}{
					map[string]interface{}{
						"term": map[string]interface{}{"isActive": true},
					},
				},
			},
		},
	}

	return r.executeSearch(ctx, query)
}

// GetFeatured retrieves featured active products
// Mirrors the C# GetFeaturedProductsAsync ES query
func (r *ElasticRepo) GetFeatured(ctx context.Context) ([]model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 10,
		"query": map[string]interface{}{
			"bool": map[string]interface{}{
				"filter": []interface{}{
					map[string]interface{}{"term": map[string]interface{}{"isActive": true}},
					map[string]interface{}{"term": map[string]interface{}{"isFeatured": true}},
				},
			},
		},
		"sort": []interface{}{
			map[string]interface{}{"createdAt": map[string]interface{}{"order": "desc"}},
		},
	}

	return r.executeSearch(ctx, query)
}

// GetByID retrieves a product by its Elasticsearch document ID
func (r *ElasticRepo) GetByID(ctx context.Context, id string) (*model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 1,
		"query": map[string]interface{}{
			"term": map[string]interface{}{
				"_id": id,
			},
		},
	}

	docs, err := r.executeSearch(ctx, query)
	if err != nil {
		return nil, err
	}
	if len(docs) == 0 {
		return nil, nil
	}
	return &docs[0], nil
}

// GetBySlug retrieves a product by its slug
func (r *ElasticRepo) GetBySlug(ctx context.Context, slug string) (*model.ProductSearchDocument, error) {
	query := map[string]interface{}{
		"size": 1,
		"query": map[string]interface{}{
			"term": map[string]interface{}{
				"slug": slug,
			},
		},
	}

	docs, err := r.executeSearch(ctx, query)
	if err != nil {
		return nil, err
	}
	if len(docs) == 0 {
		return nil, nil
	}
	return &docs[0], nil
}

// GetBrandsByKeyword searches for brands matching a keyword using wildcard aggregation
// Mirrors the C# GetBrandsByKeywordAsync
func (r *ElasticRepo) GetBrandsByKeyword(ctx context.Context, keyword string) ([]string, error) {
	query := map[string]interface{}{
		"size": 0,
		"query": map[string]interface{}{
			"wildcard": map[string]interface{}{
				"brand.keyword": map[string]interface{}{
					"value":            "*" + strings.ToLower(keyword) + "*",
					"case_insensitive": true,
				},
			},
		},
		"aggs": map[string]interface{}{
			"brands": map[string]interface{}{
				"terms": map[string]interface{}{
					"field": "brand.keyword",
					"size":  10,
				},
			},
		},
	}

	body, err := json.Marshal(query)
	if err != nil {
		return nil, err
	}

	res, err := r.client.Search(
		r.client.Search.WithContext(ctx),
		r.client.Search.WithIndex(indexName),
		r.client.Search.WithBody(bytes.NewReader(body)),
	)
	if err != nil {
		return nil, err
	}
	defer res.Body.Close()

	if res.IsError() {
		return nil, fmt.Errorf("brand search error: %s", res.Status())
	}

	var result struct {
		Aggregations struct {
			Brands aggResult `json:"brands"`
		} `json:"aggregations"`
	}
	if err := json.NewDecoder(res.Body).Decode(&result); err != nil {
		return nil, err
	}

	brands := make([]string, 0)
	for _, b := range result.Aggregations.Brands.Buckets {
		if b.Key != "" {
			brands = append(brands, b.Key)
		}
	}
	return brands, nil
}

// -------------------------------------------------------------------
// Filtering (in-memory, mirrors C# GetAllFromElasticAsync)
// -------------------------------------------------------------------

// FilterProducts applies category, price, brand, spec filters and sorting
// to a pre-fetched list of documents.
func FilterProducts(
	docs []model.ProductSearchDocument,
	categoryID string,
	categoryIDs []string, // includes descendants
	minPrice, maxPrice *float64,
	brand string,
	specs map[string]string,
	sortBy string,
) []model.ProductSearchDocument {

	filtered := make([]model.ProductSearchDocument, 0, len(docs))

	for _, d := range docs {
		// Category filter
		if len(categoryIDs) > 0 {
			found := false
			for _, cid := range categoryIDs {
				if d.CategoryID == cid {
					found = true
					break
				}
			}
			if !found {
				continue
			}
		}

		// Price filters
		if minPrice != nil && *minPrice > 0 && d.Price < *minPrice {
			continue
		}
		if maxPrice != nil && *maxPrice > 0 && d.Price > *maxPrice {
			continue
		}

		// Brand filter
		if brand != "" {
			brandMatch := strings.EqualFold(strings.TrimSpace(d.Brand), strings.TrimSpace(brand))
			nameMatch := strings.HasPrefix(
				strings.ToLower(strings.TrimSpace(d.Name)),
				strings.ToLower(strings.TrimSpace(brand))+" ",
			)
			if !brandMatch && !nameMatch {
				continue
			}
		}

		// Spec filters
		if !matchSpecs(d, specs) {
			continue
		}

		filtered = append(filtered, d)
	}

	// Sort
	switch strings.ToLower(sortBy) {
	case "price_asc":
		sort.Slice(filtered, func(i, j int) bool { return filtered[i].Price < filtered[j].Price })
	case "price_desc":
		sort.Slice(filtered, func(i, j int) bool { return filtered[i].Price > filtered[j].Price })
	case "newest":
		sort.Slice(filtered, func(i, j int) bool { return filtered[i].CreatedAt.After(filtered[j].CreatedAt) })
	default:
		sort.Slice(filtered, func(i, j int) bool { return filtered[i].CreatedAt.After(filtered[j].CreatedAt) })
	}

	return filtered
}

func matchSpecs(doc model.ProductSearchDocument, specs map[string]string) bool {
	if len(specs) == 0 {
		return true
	}

	reservedParams := map[string]bool{
		"category": true, "categoryid": true, "sortby": true,
		"minprice": true, "maxprice": true, "search": true, "brand": true,
	}

	for key, value := range specs {
		if reservedParams[strings.ToLower(key)] || value == "" {
			continue
		}
		docVal, exists := doc.Specs[key]
		if !exists {
			return false
		}
		allowedValues := strings.Split(value, ",")
		matched := false
		for _, av := range allowedValues {
			av = strings.TrimSpace(av)
			if av != "" && strings.EqualFold(docVal, av) {
				matched = true
				break
			}
		}
		if !matched {
			return false
		}
	}
	return true
}

// -------------------------------------------------------------------
// Index Operations
// -------------------------------------------------------------------

// IndexProduct indexes (upserts) a product document into Elasticsearch
func (r *ElasticRepo) IndexProduct(ctx context.Context, doc *model.ProductSearchDocument) error {
	if err := r.ensureIndex(ctx); err != nil {
		r.logger.Warn("Failed to ensure index", zap.Error(err))
	}

	body, err := json.Marshal(doc)
	if err != nil {
		return fmt.Errorf("failed to marshal document: %w", err)
	}

	res, err := r.client.Index(
		indexName,
		bytes.NewReader(body),
		r.client.Index.WithContext(ctx),
		r.client.Index.WithDocumentID(doc.ID),
	)
	if err != nil {
		return fmt.Errorf("failed to index product %s: %w", doc.ID, err)
	}
	defer res.Body.Close()

	if res.IsError() {
		return fmt.Errorf("index error for product %s: %s", doc.ID, res.Status())
	}

	r.refreshIndex(ctx)
	return nil
}

// BulkIndex indexes a batch of product documents into Elasticsearch using Bulk API
func (r *ElasticRepo) BulkIndex(ctx context.Context, docs []model.ProductSearchDocument) (int, error) {
	if len(docs) == 0 {
		return 0, nil
	}

	if err := r.ensureIndex(ctx); err != nil {
		r.logger.Warn("Failed to ensure index before bulk", zap.Error(err))
	}

	var buf bytes.Buffer
	for _, doc := range docs {
		meta := map[string]interface{}{
			"index": map[string]interface{}{
				"_index": indexName,
				"_id":    doc.ID,
			},
		}
		metaBytes, _ := json.Marshal(meta)
		docBytes, _ := json.Marshal(doc)

		buf.Write(metaBytes)
		buf.WriteByte('\n')
		buf.Write(docBytes)
		buf.WriteByte('\n')
	}

	res, err := r.client.Bulk(
		bytes.NewReader(buf.Bytes()),
		r.client.Bulk.WithContext(ctx),
		r.client.Bulk.WithIndex(indexName),
	)
	if err != nil {
		return 0, fmt.Errorf("bulk request failed: %w", err)
	}
	defer res.Body.Close()

	if res.IsError() {
		return 0, fmt.Errorf("bulk request error: %s", res.Status())
	}

	var bulkResp struct {
		Errors bool `json:"errors"`
		Items  []map[string]struct {
			Status int    `json:"status"`
			Error  *struct {
				Reason string `json:"reason"`
			} `json:"error,omitempty"`
		} `json:"items"`
	}

	if err := json.NewDecoder(res.Body).Decode(&bulkResp); err != nil {
		return len(docs), nil
	}

	succeeded := 0
	for _, item := range bulkResp.Items {
		for _, v := range item {
			if v.Status >= 200 && v.Status < 300 {
				succeeded++
			}
		}
	}

	r.refreshIndex(ctx)
	return succeeded, nil
}

// DeleteProduct removes a product document from Elasticsearch
func (r *ElasticRepo) DeleteProduct(ctx context.Context, productID string) error {
	res, err := r.client.Delete(
		indexName,
		productID,
		r.client.Delete.WithContext(ctx),
	)
	if err != nil {
		return fmt.Errorf("failed to delete product %s: %w", productID, err)
	}
	defer res.Body.Close()

	// 404 is acceptable (document was already deleted)
	if res.IsError() && res.StatusCode != 404 {
		return fmt.Errorf("delete error for product %s: %s", productID, res.Status())
	}

	r.refreshIndex(ctx)
	return nil
}

// GetIndexedCount returns the total number of documents in the index
func (r *ElasticRepo) GetIndexedCount(ctx context.Context) (int64, error) {
	res, err := r.client.Count(
		r.client.Count.WithContext(ctx),
		r.client.Count.WithIndex(indexName),
	)
	if err != nil {
		return 0, err
	}
	defer res.Body.Close()

	if res.IsError() {
		return 0, fmt.Errorf("count error: %s", res.Status())
	}

	var result struct {
		Count int64 `json:"count"`
	}
	if err := json.NewDecoder(res.Body).Decode(&result); err != nil {
		return 0, err
	}
	return result.Count, nil
}

// GetDocumentPreviews returns paginated document previews from the index
func (r *ElasticRepo) GetDocumentPreviews(ctx context.Context, page, pageSize int) ([]model.ProductSearchDocument, int64, error) {
	if page < 1 {
		page = 1
	}
	if pageSize < 10 {
		pageSize = 10
	}
	if pageSize > 100 {
		pageSize = 100
	}

	from := (page - 1) * pageSize
	query := map[string]interface{}{
		"from": from,
		"size": pageSize,
	}

	docs, err := r.executeSearch(ctx, query)
	if err != nil {
		return nil, 0, err
	}

	totalCount, _ := r.GetIndexedCount(ctx)
	return docs, totalCount, nil
}

// GetStatus returns Elasticsearch index status information
func (r *ElasticRepo) GetStatus(ctx context.Context) *model.SearchIndexStatus {
	now := time.Now().UTC().Format(time.RFC3339)
	status := &model.SearchIndexStatus{
		Status:    "Offline",
		CheckedAt: now,
	}

	if err := r.Ping(ctx); err != nil {
		errMsg := err.Error()
		status.ErrorMessage = &errMsg
		return status
	}

	count, err := r.GetIndexedCount(ctx)
	if err != nil {
		errMsg := err.Error()
		status.ErrorMessage = &errMsg
		return status
	}

	status.IsConnected = true
	status.Status = "Online"
	status.IndexedProductCount = count

	// Get last indexed time from a sample of documents
	docs, _ := r.executeSearch(ctx, map[string]interface{}{
		"size": 1,
		"sort": []interface{}{
			map[string]interface{}{"indexedAt": map[string]interface{}{"order": "desc"}},
		},
	})
	if len(docs) > 0 {
		if docs[0].IndexedAt != nil {
			t := docs[0].IndexedAt.Format(time.RFC3339)
			status.LastIndexedAt = &t
		}
	}

	return status
}

// -------------------------------------------------------------------
// Index Management
// -------------------------------------------------------------------

func (r *ElasticRepo) ensureIndex(ctx context.Context) error {
	// Check if index exists
	httpClient := &http.Client{Timeout: 5 * time.Second}
	req, _ := http.NewRequestWithContext(ctx, http.MethodHead, r.esURI+"/"+indexName, nil)
	resp, err := httpClient.Do(req)
	if err != nil {
		return err
	}
	resp.Body.Close()

	if resp.StatusCode == 200 {
		return nil // index already exists
	}

	return r.createIndex(ctx)
}

// EnsureIndex creates the index if it doesn't exist, optionally recreating it
func (r *ElasticRepo) EnsureIndex(ctx context.Context, recreate bool) error {
	httpClient := &http.Client{Timeout: 10 * time.Second}

	if recreate {
		req, _ := http.NewRequestWithContext(ctx, http.MethodDelete, r.esURI+"/"+indexName, nil)
		resp, err := httpClient.Do(req)
		if err != nil {
			return err
		}
		resp.Body.Close()
	}

	return r.createIndex(ctx)
}

func (r *ElasticRepo) createIndex(ctx context.Context) error {
	definition := getIndexDefinition()
	body, err := json.Marshal(definition)
	if err != nil {
		return err
	}

	httpClient := &http.Client{Timeout: 10 * time.Second}
	req, _ := http.NewRequestWithContext(ctx, http.MethodPut, r.esURI+"/"+indexName, bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")

	resp, err := httpClient.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode >= 400 {
		respBody, _ := io.ReadAll(resp.Body)
		// Ignore "resource_already_exists_exception"
		if !strings.Contains(string(respBody), "resource_already_exists_exception") {
			return fmt.Errorf("create index failed: %s", string(respBody))
		}
	}

	return nil
}

func (r *ElasticRepo) refreshIndex(ctx context.Context) {
	httpClient := &http.Client{Timeout: 5 * time.Second}
	req, _ := http.NewRequestWithContext(ctx, http.MethodPost, r.esURI+"/"+indexName+"/_refresh", nil)
	resp, err := httpClient.Do(req)
	if err != nil {
		r.logger.Warn("Index refresh failed", zap.Error(err))
		return
	}
	resp.Body.Close()
}

// getIndexDefinition returns the Elasticsearch index mapping definition
// This is an exact mirror of the C# GetIndexDefinitionJson in ProductIndexService.cs
func getIndexDefinition() map[string]interface{} {
	return map[string]interface{}{
		"settings": map[string]interface{}{
			"analysis": map[string]interface{}{
				"normalizer": map[string]interface{}{
					"lowercase_normalizer": map[string]interface{}{
						"type":   "custom",
						"filter": []string{"lowercase", "asciifolding"},
					},
				},
				"tokenizer": map[string]interface{}{
					"autocomplete_tokenizer": map[string]interface{}{
						"type":        "edge_ngram",
						"min_gram":    2,
						"max_gram":    20,
						"token_chars": []string{"letter", "digit"},
					},
				},
				"analyzer": map[string]interface{}{
					"autocomplete_analyzer": map[string]interface{}{
						"type":      "custom",
						"tokenizer": "autocomplete_tokenizer",
						"filter":    []string{"lowercase", "asciifolding"},
					},
					"default_search": map[string]interface{}{
						"type":      "custom",
						"tokenizer": "standard",
						"filter":    []string{"lowercase", "asciifolding"},
					},
				},
			},
		},
		"mappings": map[string]interface{}{
			"dynamic": true,
			"properties": map[string]interface{}{
				"id":   map[string]interface{}{"type": "keyword"},
				"name": map[string]interface{}{
					"type":            "text",
					"analyzer":        "autocomplete_analyzer",
					"search_analyzer": "default_search",
					"fields": map[string]interface{}{
						"keyword": map[string]interface{}{
							"type":       "keyword",
							"normalizer": "lowercase_normalizer",
						},
					},
				},
				"brand": map[string]interface{}{
					"type":            "text",
					"analyzer":        "autocomplete_analyzer",
					"search_analyzer": "default_search",
					"fields": map[string]interface{}{
						"keyword": map[string]interface{}{
							"type":       "keyword",
							"normalizer": "lowercase_normalizer",
						},
					},
				},
				"slug":          map[string]interface{}{"type": "keyword"},
				"description":   map[string]interface{}{"type": "text", "analyzer": "default_search"},
				"price":         map[string]interface{}{"type": "double"},
				"stock":         map[string]interface{}{"type": "integer"},
				"reservedStock": map[string]interface{}{"type": "integer"},
				"availableStock": map[string]interface{}{"type": "integer"},
				"isActive":      map[string]interface{}{"type": "boolean"},
				"isFeatured":    map[string]interface{}{"type": "boolean"},
				"inStock":       map[string]interface{}{"type": "boolean"},
				"imageUrls":     map[string]interface{}{"type": "keyword", "index": false},
				"createdAt":     map[string]interface{}{"type": "date"},
				"updatedAt":     map[string]interface{}{"type": "date"},
				"indexedAt":     map[string]interface{}{"type": "date"},
				"categoryId":   map[string]interface{}{"type": "keyword"},
				"categoryName": map[string]interface{}{
					"type":            "text",
					"analyzer":        "autocomplete_analyzer",
					"search_analyzer": "default_search",
					"fields": map[string]interface{}{
						"keyword": map[string]interface{}{
							"type":       "keyword",
							"normalizer": "lowercase_normalizer",
						},
					},
				},
				"categorySlug": map[string]interface{}{"type": "keyword"},
				"specs":        map[string]interface{}{"type": "flattened"},
				"specValues": map[string]interface{}{
					"type":            "text",
					"analyzer":        "autocomplete_analyzer",
					"search_analyzer": "default_search",
					"fields": map[string]interface{}{
						"keyword": map[string]interface{}{
							"type":       "keyword",
							"normalizer": "lowercase_normalizer",
						},
					},
				},
				"searchText": map[string]interface{}{
					"type":            "text",
					"analyzer":        "autocomplete_analyzer",
					"search_analyzer": "default_search",
				},
			},
		},
	}
}

// -------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------

func (r *ElasticRepo) executeSearch(ctx context.Context, query map[string]interface{}) ([]model.ProductSearchDocument, error) {
	body, err := json.Marshal(query)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal query: %w", err)
	}

	res, err := r.client.Search(
		r.client.Search.WithContext(ctx),
		r.client.Search.WithIndex(indexName),
		r.client.Search.WithBody(bytes.NewReader(body)),
	)
	if err != nil {
		return nil, fmt.Errorf("search request failed: %w", err)
	}
	defer res.Body.Close()

	if res.IsError() {
		bodyBytes, _ := io.ReadAll(res.Body)
		return nil, fmt.Errorf("search error [%s]: %s", res.Status(), string(bodyBytes))
	}

	var searchResult struct {
		Hits struct {
			Hits []struct {
				ID     string                     `json:"_id"`
				Source model.ProductSearchDocument `json:"_source"`
			} `json:"hits"`
		} `json:"hits"`
	}

	if err := json.NewDecoder(res.Body).Decode(&searchResult); err != nil {
		return nil, fmt.Errorf("failed to decode search response: %w", err)
	}

	docs := make([]model.ProductSearchDocument, 0, len(searchResult.Hits.Hits))
	for _, hit := range searchResult.Hits.Hits {
		doc := hit.Source
		// Ensure the ID is set from _id field
		if doc.ID == "" {
			doc.ID = hit.ID
		}
		docs = append(docs, doc)
	}

	return docs, nil
}

// DocumentToDTO converts a ProductSearchDocument to a ProductDTO
func DocumentToDTO(doc model.ProductSearchDocument) model.ProductDTO {
	return model.ProductDTO{
		ID:             doc.ID,
		Name:           doc.Name,
		Brand:          doc.Brand,
		Slug:           doc.Slug,
		Description:    doc.Description,
		Price:          doc.Price,
		Stock:          doc.Stock,
		ReservedStock:  doc.ReservedStock,
		AvailableStock: doc.AvailableStock,
		IsActive:       doc.IsActive,
		IsFeatured:     doc.IsFeatured,
		ImageUrls:      doc.ImageUrls,
		CreatedAt:      doc.CreatedAt.Format(time.RFC3339),
		CategoryID:     doc.CategoryID,
		CategoryName:   doc.CategoryName,
		Specs:          doc.Specs,
	}
}

// DocumentsToDTOs converts a slice of ProductSearchDocument to ProductDTO slice
func DocumentsToDTOs(docs []model.ProductSearchDocument) []model.ProductDTO {
	dtos := make([]model.ProductDTO, 0, len(docs))
	for _, doc := range docs {
		dtos = append(dtos, DocumentToDTO(doc))
	}
	return dtos
}

// DocumentToPreview converts a ProductSearchDocument to SearchIndexDocumentPreview
func DocumentToPreview(doc model.ProductSearchDocument) model.SearchIndexDocumentPreview {
	preview := model.SearchIndexDocumentPreview{
		ID:             doc.ID,
		Name:           doc.Name,
		Brand:          doc.Brand,
		Slug:           doc.Slug,
		CategoryID:     doc.CategoryID,
		CategoryName:   doc.CategoryName,
		CategorySlug:   doc.CategorySlug,
		Price:          doc.Price,
		Stock:          doc.Stock,
		ReservedStock:  doc.ReservedStock,
		AvailableStock: doc.AvailableStock,
		IsActive:       doc.IsActive,
		IsFeatured:     doc.IsFeatured,
		InStock:        doc.InStock,
		ImageUrls:      doc.ImageUrls,
		Specs:          doc.Specs,
		SpecValues:     doc.SpecValues,
		SearchText:     doc.SearchText,
		CreatedAt:      doc.CreatedAt.Format(time.RFC3339),
	}

	if doc.IndexedAt != nil {
		t := doc.IndexedAt.Format(time.RFC3339)
		preview.LastIndexedAt = &t
	} else if doc.UpdatedAt != nil {
		t := doc.UpdatedAt.Format(time.RFC3339)
		preview.LastIndexedAt = &t
	}

	return preview
}

// TotalPages calculates total pages given total count and page size
func TotalPages(totalCount int64, pageSize int) int {
	if totalCount == 0 {
		return 0
	}
	return int(math.Ceil(float64(totalCount) / float64(pageSize)))
}
