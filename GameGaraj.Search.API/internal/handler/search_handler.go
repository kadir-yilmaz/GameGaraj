package handler

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"
	"go.uber.org/zap"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/service"
)

// SearchHandler handles search, suggestions, and facets endpoints
type SearchHandler struct {
	service    *service.SearchService
	catalogURL string
	logger     *zap.Logger
}

// NewSearchHandler creates a new search handler
func NewSearchHandler(svc *service.SearchService, catalogURL string, logger *zap.Logger) *SearchHandler {
	return &SearchHandler{
		service:    svc,
		catalogURL: catalogURL,
		logger:     logger,
	}
}

// Search godoc
// @Summary Full-text Product Search
// @Description Searches active products across name, brand, category, specs, and description with fuzzy matching and Redis caching
// @Tags Search
// @Accept json
// @Produce json
// @Param q query string true "Search keyword"
// @Success 200 {array} model.ProductDTO
// @Failure 500 {object} map[string]string
// @Router /api/search [get]
func (h *SearchHandler) Search(c *gin.Context) {
	keyword := c.Query("q")

	results, err := h.service.Search(c.Request.Context(), keyword)
	if err != nil {
		h.logger.Error("Search failed", zap.String("keyword", keyword), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Search failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// Suggestions godoc
// @Summary Autocomplete Search Suggestions
// @Description Returns autocomplete suggestions including products, matching brands, and categories
// @Tags Search
// @Accept json
// @Produce json
// @Param q query string true "Prefix or keyword"
// @Success 200 {array} model.SearchSuggestion
// @Failure 500 {object} map[string]string
// @Router /api/search/suggestions [get]
func (h *SearchHandler) Suggestions(c *gin.Context) {
	keyword := c.Query("q")

	results, err := h.service.Suggestions(c.Request.Context(), keyword)
	if err != nil {
		h.logger.Error("Suggestions failed", zap.String("keyword", keyword), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Suggestions failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// Facets godoc
// @Summary Search Facets Aggregation
// @Description Returns brand and category facet counts, optionally filtered by keyword
// @Tags Search
// @Accept json
// @Produce json
// @Param q query string false "Filter keyword"
// @Success 200 {object} model.SearchFacetResult
// @Failure 500 {object} map[string]string
// @Router /api/search/facets [get]
func (h *SearchHandler) Facets(c *gin.Context) {
	keyword := c.DefaultQuery("q", "")

	results, err := h.service.Facets(c.Request.Context(), keyword)
	if err != nil {
		h.logger.Error("Facets failed", zap.String("keyword", keyword), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Facets failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// Brands godoc
// @Summary Brand Suggestions
// @Description Returns brand names matching a wildcard query
// @Tags Search
// @Accept json
// @Produce json
// @Param q query string true "Brand keyword"
// @Success 200 {array} string
// @Failure 500 {object} map[string]string
// @Router /api/search/brands [get]
func (h *SearchHandler) Brands(c *gin.Context) {
	keyword := c.Query("q")

	results, err := h.service.Brands(c.Request.Context(), keyword)
	if err != nil {
		h.logger.Error("Brands failed", zap.String("keyword", keyword), zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Brands failed"})
		return
	}

	c.JSON(http.StatusOK, results)
}

// Reindex godoc
// @Summary Full Reindex from Catalog API
// @Description Fetches all products from Catalog API, bulk indexes into Elasticsearch, and purges Redis search caches
// @Tags Index Management
// @Accept json
// @Produce json
// @Success 200 {object} model.ReindexResult
// @Failure 500 {object} map[string]string
// @Router /api/search/reindex [post]
func (h *SearchHandler) Reindex(c *gin.Context) {
	result, err := h.service.ReindexFromCatalog(c.Request.Context(), h.catalogURL)
	if err != nil {
		h.logger.Error("Reindex failed", zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
		return
	}

	c.JSON(http.StatusOK, result)
}

// Status godoc
// @Summary Elasticsearch Index Status
// @Description Returns health, connection status, document count, and last indexed timestamp
// @Tags Index Management
// @Accept json
// @Produce json
// @Success 200 {object} model.SearchIndexStatus
// @Router /api/search/status [get]
func (h *SearchHandler) Status(c *gin.Context) {
	status := h.service.GetStatus(c.Request.Context())
	c.JSON(http.StatusOK, status)
}

// Documents godoc
// @Summary Search Index Document Previews
// @Description Returns paginated preview of raw indexed documents for dashboard and debugging
// @Tags Index Management
// @Accept json
// @Produce json
// @Param page query int false "Page number (default 1)"
// @Param pageSize query int false "Page size (default 100, max 100)"
// @Success 200 {object} model.PagedResult
// @Failure 500 {object} map[string]string
// @Router /api/search/documents [get]
func (h *SearchHandler) Documents(c *gin.Context) {
	page, _ := strconv.Atoi(c.DefaultQuery("page", "1"))
	pageSize, _ := strconv.Atoi(c.DefaultQuery("pageSize", "100"))

	result, err := h.service.GetDocumentPreviews(c.Request.Context(), page, pageSize)
	if err != nil {
		h.logger.Error("Document previews failed", zap.Error(err))
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Document previews failed"})
		return
	}

	c.JSON(http.StatusOK, result)
}
