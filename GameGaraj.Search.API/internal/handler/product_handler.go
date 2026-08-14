package handler

import (
	"net/http"
	"strconv"
	"strings"

	"github.com/gin-gonic/gin"
	"go.uber.org/zap"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/service"
)

// ProductHandler handles product read endpoints (featured, byId, bySlug, filter)
type ProductHandler struct {
	service *service.SearchService
	logger  *zap.Logger
}

// NewProductHandler creates a new product handler
func NewProductHandler(svc *service.SearchService, logger *zap.Logger) *ProductHandler {
	return &ProductHandler{
		service: svc,
		logger:  logger,
	}
}

// GetFiltered godoc
// @Summary Filtered Product Listing
// @Description Returns products filtered by category, price range, brand, dynamic specs, and sorted
// @Tags Products
// @Accept json
// @Produce json
// @Param categoryId query string false "Category ID"
// @Param sortBy query string false "Sort order (price_asc, price_desc, newest)"
// @Param minPrice query number false "Minimum price"
// @Param maxPrice query number false "Maximum price"
// @Param brand query string false "Brand name"
// @Success 200 {array} model.ProductDTO
// @Failure 500 {object} map[string]string
// @Router /api/search/products [get]
func (h *ProductHandler) GetFiltered(c *gin.Context) {
	categoryID := c.Query("categoryId")
	sortBy := c.Query("sortBy")
	brand := c.Query("brand")

	var minPrice, maxPrice *float64
	if v := c.Query("minPrice"); v != "" {
		if parsed, err := strconv.ParseFloat(v, 64); err == nil {
			minPrice = &parsed
		}
	}
	if v := c.Query("maxPrice"); v != "" {
		if parsed, err := strconv.ParseFloat(v, 64); err == nil {
			maxPrice = &parsed
		}
	}

	specs := make(map[string]string)
	for key, values := range c.Request.URL.Query() {
		lowerKey := strings.ToLower(key)
		if lowerKey == "categoryid" || lowerKey == "sortby" || lowerKey == "minprice" ||
			lowerKey == "maxprice" || lowerKey == "brand" || lowerKey == "search" ||
			lowerKey == "category" {
			continue
		}
		if len(values) > 0 {
			specs[key] = values[0]
		}
	}

	var categoryIDs []string
	if categoryID != "" {
		categoryIDs = []string{categoryID}
	}

	results, err := h.service.GetFiltered(c.Request.Context(), categoryID, categoryIDs, sortBy, minPrice, maxPrice, brand, specs)
	if err != nil {
		h.logger.Error("Filtered products failed", zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Product listing failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// GetFeatured godoc
// @Summary Featured Products
// @Description Returns active featured products from Elasticsearch with Redis cache (TTL 10m)
// @Tags Products
// @Accept json
// @Produce json
// @Success 200 {array} model.ProductDTO
// @Failure 500 {object} map[string]string
// @Router /api/search/products/featured [get]
func (h *ProductHandler) GetFeatured(c *gin.Context) {
	results, err := h.service.GetFeatured(c.Request.Context())
	if err != nil {
		h.logger.Error("Featured products failed", zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Featured products failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// GetByID godoc
// @Summary Get Product by ID
// @Description Returns a single product document from Elasticsearch with Redis cache (TTL 10m)
// @Tags Products
// @Accept json
// @Produce json
// @Param id path string true "Product ID"
// @Success 200 {object} model.ProductDTO
// @Failure 404 {object} map[string]string
// @Failure 500 {object} map[string]string
// @Router /api/search/products/{id} [get]
func (h *ProductHandler) GetByID(c *gin.Context) {
	id := c.Param("id")

	result, err := h.service.GetByID(c.Request.Context(), id)
	if err != nil {
		h.logger.Error("GetByID failed", zap.String("id", id), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Product lookup failed"})
		return
	}

	if result == nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Urun bulunamadi."})
		return
	}

	c.JSON(http.StatusOK, result)
}

// GetBySlug godoc
// @Summary Get Product by Slug
// @Description Returns a single product document from Elasticsearch by slug with Redis cache (TTL 10m)
// @Tags Products
// @Accept json
// @Produce json
// @Param slug path string true "Product Slug"
// @Success 200 {object} model.ProductDTO
// @Failure 404 {object} map[string]string
// @Failure 500 {object} map[string]string
// @Router /api/search/products/slug/{slug} [get]
func (h *ProductHandler) GetBySlug(c *gin.Context) {
	slug := c.Param("slug")

	result, err := h.service.GetBySlug(c.Request.Context(), slug)
	if err != nil {
		h.logger.Error("GetBySlug failed", zap.String("slug", slug), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Product lookup failed"})
		return
	}

	if result == nil {
		c.JSON(http.StatusNotFound, gin.H{"message": "Urun bulunamadi."})
		return
	}

	c.JSON(http.StatusOK, result)
}
