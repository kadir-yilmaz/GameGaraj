package main

import (
	"context"
	"errors"
	"log"
	"net/http"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	_ "gamegaraj-notification-api/docs"
	"gamegaraj-notification-api/internal/config"
	"gamegaraj-notification-api/internal/queue"
	"gamegaraj-notification-api/internal/service"
	"gamegaraj-notification-api/internal/storage"
	"gamegaraj-notification-api/internal/telemetry"

	"github.com/gin-gonic/gin"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promhttp"
	swaggerFiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"
)

var httpServerRequestDuration = prometheus.NewHistogramVec(
	prometheus.HistogramOpts{
		Name:        "http_server_request_duration_seconds",
		Help:        "Duration of inbound HTTP server requests.",
		ConstLabels: prometheus.Labels{"service_name": "GameGaraj.Notification"},
		Buckets:     prometheus.DefBuckets,
	},
	[]string{"http_request_method", "http_response_status_code", "http_route"},
)

func init() {
	prometheus.MustRegister(httpServerRequestDuration)
}

// @title GameGaraj Notification API
// @version 1.0
// @description Go-based event-driven notification service for GameGaraj managing Email, SMS, and MinIO storage.
// @contact.name Kadir Yılmaz
// @host localhost:5025
// @BasePath /
func main() {
	log.Println("[Main] Starting Go Notification Service...")

	// 1. Load Configuration
	cfg := config.LoadConfig()

	// 2. Initialize context with cancel support for graceful shutdown
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	// 3. Initialize OpenTelemetry Tracing
	otelShutdown, err := telemetry.InitTracer(ctx, cfg)
	if err != nil {
		log.Printf("[Telemetry] ⚠️ Warning: Failed to initialize OpenTelemetry Tracing: %v", err)
	} else {
		defer func() {
			if err := otelShutdown(context.Background()); err != nil {
				log.Printf("[Telemetry] Error shutting down TraceProvider: %v", err)
			}
		}()
	}

	// 4. Initialize Services and Storage
	minioClient, err := storage.NewMinioClient(cfg)
	if err != nil {
		log.Fatalf("[Main] ❌ Failed to initialize MinIO Client: %v", err)
	}

	emailService := service.NewEmailService(cfg)
	smsService := service.NewSmsService()

	// 5. Initialize and Start RabbitMQ Consumer
	consumer, err := queue.NewConsumer(cfg, emailService, smsService, minioClient)
	if err != nil {
		log.Fatalf("[Main] ❌ Failed to initialize RabbitMQ Consumer: %v", err)
	}
	defer consumer.Close()

	err = consumer.Start(ctx)
	if err != nil {
		log.Fatalf("[Main] ❌ Failed to start RabbitMQ Consumer: %v", err)
	}

	// 6. Initialize Web Server (Gin)
	if cfg.Environment == "Production" {
		gin.SetMode(gin.ReleaseMode)
	}

	router := gin.New()
	router.Use(gin.Recovery())
	router.Use(recordHTTPMetrics())

	// Add simple structured log middleware for Gin
	router.Use(func(c *gin.Context) {
		start := time.Now()
		path := c.Request.URL.Path
		raw := c.Request.URL.RawQuery

		c.Next()

		latency := time.Since(start)
		statusCode := c.Writer.Status()

		if path != "/health" && path != "/metrics" && path != "/swagger/index.html" {
			log.Printf("[HTTP] %s %s %s %d %s", c.Request.Method, path, raw, statusCode, latency)
		}
	})

	// Swagger UI
	router.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerFiles.Handler))

	// Expose Health checks
	router.GET("/health", healthCheckHandler)

	// Expose Prometheus Metrics endpoint
	router.GET("/metrics", gin.WrapH(promhttp.Handler()))

	// 7. Run HTTP Server in a goroutine
	srv := &http.Server{
		Addr:    ":" + cfg.Port,
		Handler: router,
	}

	go func() {
		log.Printf("[HTTP] Web Server running on port %s", cfg.Port)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Fatalf("[HTTP] ❌ Web Server failed: %v", err)
		}
	}()

	// 8. Graceful Shutdown Wait
	<-ctx.Done()
	log.Println("[Main] Shutdown signal received. Shutting down gracefully...")

	// Shut down HTTP Server with timeout
	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	if err := srv.Shutdown(shutdownCtx); err != nil {
		log.Printf("[HTTP] ❌ Web Server forced to shutdown: %v", err)
	} else {
		log.Println("[HTTP] Web Server stopped successfully.")
	}

	log.Println("[Main] Go Notification Service stopped.")
}

// HealthCheckResponse represents health check response
type HealthCheckResponse struct {
	Status    string `json:"status" example:"healthy"`
	Timestamp string `json:"timestamp" example:"2026-08-14T19:00:00Z"`
	Service   string `json:"service" example:"notification-service"`
}

// healthCheckHandler godoc
// @Summary Service Health Check
// @Description Returns the service health status and timestamp
// @Tags Health
// @Produce json
// @Success 200 {object} HealthCheckResponse
// @Router /health [get]
func healthCheckHandler(c *gin.Context) {
	c.JSON(http.StatusOK, HealthCheckResponse{
		Status:    "healthy",
		Timestamp: time.Now().Format(time.RFC3339),
		Service:   "notification-service",
	})
}

func recordHTTPMetrics() gin.HandlerFunc {
	return func(c *gin.Context) {
		start := time.Now()

		c.Next()

		if c.Request.URL.Path == "/metrics" {
			return
		}

		route := c.FullPath()
		if route == "" {
			route = c.Request.URL.Path
		}

		httpServerRequestDuration.WithLabelValues(
			c.Request.Method,
			strconv.Itoa(c.Writer.Status()),
			route,
		).Observe(time.Since(start).Seconds())
	}
}
