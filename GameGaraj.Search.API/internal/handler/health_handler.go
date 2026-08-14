package handler

import (
	"net/http"

	"github.com/gin-gonic/gin"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/cache"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/repository"
)

// HealthHandler handles health check endpoints
type HealthHandler struct {
	esRepo *repository.ElasticRepo
	cache  *cache.Service
}

// NewHealthHandler creates a new health handler
func NewHealthHandler(esRepo *repository.ElasticRepo, cache *cache.Service) *HealthHandler {
	return &HealthHandler{
		esRepo: esRepo,
		cache:  cache,
	}
}

// Health godoc
// @Summary Service Health Check
// @Description Checks connectivity to Elasticsearch and Redis
// @Tags Health
// @Produce json
// @Success 200 {object} map[string]string
// @Failure 503 {object} map[string]string
// @Router /api/health [get]
func (h *HealthHandler) Health(c *gin.Context) {
	esErr := h.esRepo.Ping(c.Request.Context())
	redisErr := h.cache.Ping(c.Request.Context())

	esStatus := "healthy"
	if esErr != nil {
		esStatus = "unhealthy: " + esErr.Error()
	}

	redisStatus := "healthy"
	if redisErr != nil {
		redisStatus = "unhealthy: " + redisErr.Error()
	}

	status := http.StatusOK
	overallStatus := "healthy"
	if esErr != nil {
		status = http.StatusServiceUnavailable
		overallStatus = "degraded"
	}

	c.JSON(status, gin.H{
		"status":        overallStatus,
		"service":       "GameGaraj.Search.API",
		"elasticsearch": esStatus,
		"redis":         redisStatus,
	})
}
