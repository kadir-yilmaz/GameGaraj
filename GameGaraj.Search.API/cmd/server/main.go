package main

import (
	"context"
	"fmt"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/gin-gonic/gin"
	swaggerFiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"
	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"

	_ "github.com/kadir-yilmaz/gamegaraj-search-api/docs"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/cache"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/config"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/consumer"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/handler"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/repository"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/service"
)

// @title GameGaraj Search API
// @version 1.0
// @description High-performance Go + Gin search service for GameGaraj powered by Elasticsearch and Redis Cache.
// @contact.name Kadir Yılmaz
// @host localhost:5082
// @BasePath /
func main() {
	// Logger
	logConfig := zap.NewProductionConfig()
	logConfig.EncoderConfig.TimeKey = "timestamp"
	logConfig.EncoderConfig.EncodeTime = zapcore.ISO8601TimeEncoder
	logger, err := logConfig.Build()
	if err != nil {
		fmt.Printf("Failed to create logger: %v\n", err)
		os.Exit(1)
	}
	defer logger.Sync()

	logger.Info("Starting GameGaraj Search API...")

	// Config
	cfg := config.Load()
	logger.Info("Configuration loaded",
		zap.String("port", cfg.Server.Port),
		zap.String("elasticsearch", cfg.Elasticsearch.URI),
		zap.String("redis", cfg.Redis.Address),
		zap.String("rabbitmq", cfg.RabbitMQ.URL))

	// Redis Cache
	cacheService := cache.NewService(
		cfg.Redis.Address,
		cfg.Redis.Password,
		cfg.Redis.DB,
		cfg.Redis.InstanceName,
		logger,
	)
	defer cacheService.Close()

	// Check Redis connectivity
	ctx := context.Background()
	if err := cacheService.Ping(ctx); err != nil {
		logger.Warn("Redis is not available, running without cache", zap.Error(err))
	} else {
		logger.Info("Redis connected successfully")
	}

	// Elasticsearch Repository
	esRepo, err := repository.NewElasticRepo(cfg.Elasticsearch.URI, logger)
	if err != nil {
		logger.Fatal("Failed to create Elasticsearch client", zap.Error(err))
	}

	// Check ES connectivity
	if err := esRepo.Ping(ctx); err != nil {
		logger.Warn("Elasticsearch is not available", zap.Error(err))
	} else {
		logger.Info("Elasticsearch connected successfully")
		// Ensure index exists
		if err := esRepo.EnsureIndex(ctx, false); err != nil {
			logger.Warn("Failed to ensure Elasticsearch index", zap.Error(err))
		}
	}

	// Search Service
	searchService := service.NewSearchService(esRepo, cacheService, logger)

	// Handlers
	searchHandler := handler.NewSearchHandler(searchService, cfg.Catalog.URL, logger)
	productHandler := handler.NewProductHandler(searchService, logger)
	healthHandler := handler.NewHealthHandler(esRepo, cacheService)

	// Auto Initial Sync: If Elasticsearch is empty, automatically sync from Catalog API in background
	go func() {
		time.Sleep(3 * time.Second) // wait for server to fully initialize
		initCtx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
		defer cancel()

		count, err := esRepo.GetIndexedCount(initCtx)
		if err == nil && count == 0 {
			logger.Info("Elasticsearch index is empty. Triggering automatic initial sync from Catalog API...",
				zap.String("catalogURL", cfg.Catalog.URL))
			result, syncErr := searchService.ReindexFromCatalog(initCtx, cfg.Catalog.URL)
			if syncErr != nil {
				logger.Warn("Auto initial sync failed (Catalog API may not be running yet). Will retry on demand via /api/search/reindex.",
					zap.Error(syncErr))
			} else {
				logger.Info("Auto initial sync completed successfully!",
					zap.Int("totalIndexed", result.Succeeded))
			}
		}
	}()

	// Gin Router
	gin.SetMode(gin.ReleaseMode)
	router := gin.New()
	router.Use(gin.Recovery())
	router.Use(corsMiddleware())
	router.Use(requestLogger(logger))

	// Swagger UI endpoint
	router.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerFiles.Handler))

	// Routes
	api := router.Group("/api")
	{
		// Health
		api.GET("/health", healthHandler.Health)

		// Search endpoints
		search := api.Group("/search")
		{
			search.GET("", searchHandler.Search)
			search.GET("/suggestions", searchHandler.Suggestions)
			search.GET("/facets", searchHandler.Facets)
			search.GET("/brands", searchHandler.Brands)
			search.POST("/reindex", searchHandler.Reindex)
			search.GET("/status", searchHandler.Status)
			search.GET("/documents", searchHandler.Documents)

			// Product read endpoints (via Elasticsearch)
			products := search.Group("/products")
			{
				products.GET("", productHandler.GetFiltered)
				products.GET("/featured", productHandler.GetFeatured)
				products.GET("/slug/:slug", productHandler.GetBySlug)
				products.GET("/:id", productHandler.GetByID)
			}
		}
	}

	// Context with cancellation for graceful shutdown
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// Start RabbitMQ Consumer in a goroutine
	rmqConsumer := consumer.NewRabbitMQConsumer(
		cfg.RabbitMQ.URL,
		cfg.RabbitMQ.Username,
		cfg.RabbitMQ.Password,
		searchService,
		logger,
	)

	go func() {
		if err := rmqConsumer.Start(ctx); err != nil {
			logger.Error("RabbitMQ consumer failed", zap.Error(err))
		}
	}()

	// HTTP Server
	srv := &http.Server{
		Addr:         cfg.Server.Port,
		Handler:      router,
		ReadTimeout:  15 * time.Second,
		WriteTimeout: 15 * time.Second,
		IdleTimeout:  60 * time.Second,
	}

	// Start server in a goroutine
	go func() {
		logger.Info("HTTP server starting", zap.String("port", cfg.Server.Port))
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logger.Fatal("HTTP server failed", zap.Error(err))
		}
	}()

	// Graceful Shutdown
	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	sig := <-quit
	logger.Info("Shutdown signal received", zap.String("signal", sig.String()))

	// Cancel context to stop RabbitMQ consumer
	cancel()

	// Shutdown HTTP server with timeout
	shutdownCtx, shutdownCancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer shutdownCancel()

	if err := srv.Shutdown(shutdownCtx); err != nil {
		logger.Error("HTTP server shutdown error", zap.Error(err))
	}

	logger.Info("GameGaraj Search API stopped gracefully")
}

// corsMiddleware adds CORS headers for development
func corsMiddleware() gin.HandlerFunc {
	return func(c *gin.Context) {
		c.Header("Access-Control-Allow-Origin", "*")
		c.Header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
		c.Header("Access-Control-Allow-Headers", "Content-Type, Authorization")

		if c.Request.Method == "OPTIONS" {
			c.AbortWithStatus(http.StatusNoContent)
			return
		}

		c.Next()
	}
}

// requestLogger logs HTTP requests with structured fields
func requestLogger(logger *zap.Logger) gin.HandlerFunc {
	return func(c *gin.Context) {
		start := time.Now()
		path := c.Request.URL.Path
		query := c.Request.URL.RawQuery

		c.Next()

		duration := time.Since(start)
		statusCode := c.Writer.Status()

		if path == "/api/health" || path == "/swagger/index.html" {
			return
		}

		logger.Info("HTTP Request",
			zap.Int("status", statusCode),
			zap.String("method", c.Request.Method),
			zap.String("path", path),
			zap.String("query", query),
			zap.Duration("duration", duration),
			zap.String("clientIP", c.ClientIP()),
		)
	}
}
