package model

import "time"

// ProductSearchDocument represents a product document stored in Elasticsearch.
// This mirrors the C# ProductSearchDocument in Catalog.API/Models/ProductSearchDocument.cs
type ProductSearchDocument struct {
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
	InStock        bool              `json:"inStock"`
	ImageUrls      []string          `json:"imageUrls"`
	CreatedAt      time.Time         `json:"createdAt"`
	UpdatedAt      *time.Time        `json:"updatedAt,omitempty"`
	IndexedAt      *time.Time        `json:"indexedAt,omitempty"`
	CategoryID     string            `json:"categoryId"`
	CategoryName   string            `json:"categoryName"`
	CategorySlug   string            `json:"categorySlug"`
	Specs          map[string]string `json:"specs"`
	SpecValues     []string          `json:"specValues"`
	SearchText     string            `json:"searchText"`
}

// ProductEvent represents the message received from RabbitMQ
// when a product is created, updated, or deleted in Catalog API.
type ProductEvent struct {
	ID             string            `json:"id"`
	Name           string            `json:"name"`
	Brand          string            `json:"brand"`
	Slug           string            `json:"slug"`
	Description    string            `json:"description"`
	Price          float64           `json:"price"`
	Stock          int               `json:"stock"`
	ReservedStock  int               `json:"reservedStock"`
	IsActive       bool              `json:"isActive"`
	IsFeatured     bool              `json:"isFeatured"`
	ImageUrls      []string          `json:"imageUrls"`
	CategoryID     string            `json:"categoryId"`
	CategoryName   string            `json:"categoryName"`
	CategorySlug   string            `json:"categorySlug"`
	Specs          map[string]string `json:"specs"`
}

// ToSearchDocument converts a ProductEvent to a ProductSearchDocument
// for indexing into Elasticsearch.
func (e *ProductEvent) ToSearchDocument() *ProductSearchDocument {
	availableStock := e.Stock - e.ReservedStock

	specValues := make([]string, 0, len(e.Specs))
	seen := make(map[string]bool)
	for _, v := range e.Specs {
		trimmed := v
		if trimmed != "" && !seen[trimmed] {
			specValues = append(specValues, trimmed)
			seen[trimmed] = true
		}
	}

	// Build searchText from all searchable fields
	parts := []string{e.Name, e.Brand, e.CategoryName, e.Description}
	for k := range e.Specs {
		parts = append(parts, k)
	}
	parts = append(parts, specValues...)

	searchText := ""
	for _, p := range parts {
		if p != "" {
			if searchText != "" {
				searchText += " "
			}
			searchText += p
		}
	}

	now := time.Now().UTC()
	return &ProductSearchDocument{
		ID:             e.ID,
		Name:           e.Name,
		Brand:          e.Brand,
		Slug:           e.Slug,
		Description:    e.Description,
		Price:          e.Price,
		Stock:          e.Stock,
		ReservedStock:  e.ReservedStock,
		AvailableStock: availableStock,
		IsActive:       e.IsActive,
		IsFeatured:     e.IsFeatured,
		InStock:        availableStock > 0,
		ImageUrls:      e.ImageUrls,
		CreatedAt:      now,
		IndexedAt:      &now,
		CategoryID:     e.CategoryID,
		CategoryName:   e.CategoryName,
		CategorySlug:   e.CategorySlug,
		Specs:          e.Specs,
		SpecValues:     specValues,
		SearchText:     searchText,
	}
}
