package queue

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"strings"
	"time"

	"gamegaraj-notification-api/internal/config"
	"gamegaraj-notification-api/internal/service"
	"gamegaraj-notification-api/internal/storage"

	amqp "github.com/rabbitmq/amqp091-go"
	"github.com/redis/go-redis/v9"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/trace"
)

const (
	exchangeName = "GameGaraj.Shared.Events:SendNotification"
	queueName    = "notification-service"
	tracerName   = "notification-rabbitmq-consumer"
)

type SendNotificationMessage struct {
	Recipient      string `json:"recipient"`
	Type           string `json:"type"`
	Title          string `json:"title"`
	Body           string `json:"body"`
	AttachmentPath string `json:"attachmentPath"`
	AttachmentName string `json:"attachmentName"`
}

type MassTransitEnvelope struct {
	Message SendNotificationMessage `json:"message"`
}

type Consumer struct {
	conn         *amqp.Connection
	channel      *amqp.Channel
	emailService service.EmailService
	smsService   service.SmsService
	minioClient  *storage.MinioClient
	rdb          *redis.Client
	tracer       trace.Tracer
}

func NewConsumer(
	cfg *config.Config,
	emailService service.EmailService,
	smsService service.SmsService,
	minioClient *storage.MinioClient,
) (*Consumer, error) {
	amqpURL := fmt.Sprintf("amqp://guest:guest@%s:5672/", cfg.RabbitMQURL)
	log.Printf("[RabbitMQ] Connecting to RabbitMQ at: %s", amqpURL)

	conn, err := amqp.Dial(amqpURL)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to RabbitMQ: %w", err)
	}

	channel, err := conn.Channel()
	if err != nil {
		conn.Close()
		return nil, fmt.Errorf("failed to open a channel: %w", err)
	}

	// Declare exchange (fanout, matches MassTransit publish style)
	err = channel.ExchangeDeclare(
		exchangeName,
		"fanout",
		true,  // durable
		false, // auto-deleted
		false, // internal
		false, // no-wait
		nil,   // arguments
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("failed to declare exchange: %w", err)
	}

	// Declare queue
	_, err = channel.QueueDeclare(
		queueName,
		true,  // durable
		false, // delete when unused
		false, // exclusive
		false, // no-wait
		nil,   // arguments
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("failed to declare queue: %w", err)
	}

	// Bind queue to exchange
	err = channel.QueueBind(
		queueName,
		"", // routing key (ignored for fanout)
		exchangeName,
		false,
		nil,
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("failed to bind queue to exchange: %w", err)
	}

	tracer := otel.Tracer(tracerName)

	var rdb *redis.Client
	if cfg.RedisURL != "" {
		rdb = redis.NewClient(&redis.Options{
			Addr: cfg.RedisURL,
		})
	}

	return &Consumer{
		conn:         conn,
		channel:      channel,
		emailService: emailService,
		smsService:   smsService,
		minioClient:  minioClient,
		rdb:          rdb,
		tracer:       tracer,
	}, nil
}

func (c *Consumer) Start(ctx context.Context) error {
	msgs, err := c.channel.Consume(
		queueName,
		"",    // consumer tag
		false, // auto-ack (disabled for safety, we acknowledge manually)
		false, // exclusive
		false, // no-local
		false, // no-wait
		nil,   // args
	)
	if err != nil {
		return fmt.Errorf("failed to start consuming: %w", err)
	}

	log.Printf("[RabbitMQ] Consumer started. Listening on queue: %s", queueName)

	go func() {
		for {
			select {
			case <-ctx.Done():
				log.Println("[RabbitMQ] Context cancelled, stopping consumer...")
				return
			case d, ok := <-msgs:
				if !ok {
					log.Println("[RabbitMQ] Message channel closed, stopping consumer...")
					return
				}

				c.processMessage(ctx, d)
			}
		}
	}()

	return nil
}

func (c *Consumer) processMessage(ctx context.Context, d amqp.Delivery) {
	// Start trace span
	_, span := c.tracer.Start(ctx, "rabbitmq.consume", trace.WithSpanKind(trace.SpanKindConsumer))
	defer span.End()

	log.Printf("[RabbitMQ] Received a message from RabbitMQ (Size: %d bytes)", len(d.Body))

	// 💤 Chaos Manager Check for Notification.API (Uyku Modu & Gecikme)
	if c.rdb != nil {
		ctxTimeout, cancel := context.WithTimeout(ctx, 1*time.Second)
		val, err := c.rdb.Get(ctxTimeout, "chaos:rule:notification").Result()
		cancel()
		if err == nil && val != "" {
			var rule struct {
				Enabled    bool `json:"enabled"`
				AlwaysFail bool `json:"alwaysFail"`
				LatencyMs  int  `json:"latencyMs"`
			}
			if err := json.Unmarshal([]byte(val), &rule); err == nil && rule.Enabled {
				if rule.AlwaysFail {
					log.Printf("[Chaos] 💤 Notification.API UYKU MODUNDA! E-posta bekletiliyor, mesaj kuyruğa iade edildi (Nack requeue)...")
					time.Sleep(3 * time.Second)
					_ = d.Nack(false, true)
					return
				}
				if rule.LatencyMs > 0 {
					log.Printf("[Chaos] ⏱️ Notification.API %d ms gecikme uygulanıyor...", rule.LatencyMs)
					time.Sleep(time.Duration(rule.LatencyMs) * time.Millisecond)
				}
			}
		}
	}

	var envelope MassTransitEnvelope
	if err := json.Unmarshal(d.Body, &envelope); err != nil {
		log.Printf("[RabbitMQ] ❌ Failed to parse JSON envelope: %v", err)
		span.RecordError(err)
		// Reject and discard corrupt message
		_ = d.Reject(false)
		return
	}

	msg := envelope.Message
	span.SetAttributes(
		attribute.String("notification.type", msg.Type),
		attribute.String("notification.recipient", msg.Recipient),
		attribute.String("notification.title", msg.Title),
	)

	log.Printf("[RabbitMQ] Processing Notification. Type: %s, Recipient: %s", msg.Type, msg.Recipient)

	var err error
	switch msg.Type {
	case "Email":
		var attachmentBytes []byte
		if msg.AttachmentPath != "" {
			span.AddEvent("downloading_attachment", trace.WithAttributes(attribute.String("path", msg.AttachmentPath)))
			
			// 1. Check local storage on disk (Invoice.API wwwroot/invoices)
			cleanPath := strings.TrimPrefix(msg.AttachmentPath, "/")
			possiblePaths := []string{
				cleanPath,
				filepath.Join("..", "GameGaraj.Invoice.API", "wwwroot", cleanPath),
				filepath.Join("..", "..", "GameGaraj.Invoice.API", "wwwroot", cleanPath),
				filepath.Join("GameGaraj.Invoice.API", "wwwroot", cleanPath),
			}

			for _, p := range possiblePaths {
				if data, readErr := os.ReadFile(p); readErr == nil && len(data) > 0 {
					attachmentBytes = data
					log.Printf("[Storage] ✅ Invoice PDF loaded from local disk: %s (%d bytes)", p, len(data))
					break
				}
			}

			// 2. If not found on local disk, try MinIO S3 object storage
			if len(attachmentBytes) == 0 && c.minioClient != nil {
				minioCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
				var minioErr error
				attachmentBytes, minioErr = c.minioClient.DownloadFile(minioCtx, msg.AttachmentPath)
				cancel()
				if minioErr != nil {
					log.Printf("[Storage] ⚠️ Could not fetch attachment from MinIO (%v), will proceed sending email without attachment...", minioErr)
				}
			}
		}

		emailCtx, cancel := context.WithTimeout(ctx, 20*time.Second)
		err = c.emailService.SendEmail(emailCtx, msg.Recipient, msg.Title, msg.Body, attachmentBytes, msg.AttachmentName)
		cancel()

	case "SMS":
		smsCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
		err = c.smsService.SendSms(smsCtx, msg.Recipient, msg.Body)
		cancel()

	default:
		err = fmt.Errorf("unknown notification type: %s", msg.Type)
	}

	if err != nil {
		log.Printf("[RabbitMQ] ❌ Failed to process notification: %v", err)
		span.RecordError(err)
		// Reject and requeue so it can be retried
		_ = d.Nack(false, true)
		return
	}

	// Successfully processed
	log.Printf("[RabbitMQ] ✅ Notification successfully sent to: %s", msg.Recipient)
	_ = d.Ack(false)
}

func (c *Consumer) Close() {
	if c.channel != nil {
		_ = c.channel.Close()
	}
	if c.conn != nil {
		_ = c.conn.Close()
	}
	log.Println("[RabbitMQ] Connection closed.")
}
