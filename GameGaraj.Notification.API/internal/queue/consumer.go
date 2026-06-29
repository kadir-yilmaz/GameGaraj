package queue

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"time"

	"gamegaraj-notification-api/internal/config"
	"gamegaraj-notification-api/internal/service"
	"gamegaraj-notification-api/internal/storage"

	amqp "github.com/rabbitmq/amqp091-go"
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

	return &Consumer{
		conn:         conn,
		channel:      channel,
		emailService: emailService,
		smsService:   smsService,
		minioClient:  minioClient,
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
			
			// Download attachment from MinIO
			minioCtx, cancel := context.WithTimeout(ctx, 15*time.Second)
			attachmentBytes, err = c.minioClient.DownloadFile(minioCtx, msg.AttachmentPath)
			cancel()
			if err != nil {
				log.Printf("[RabbitMQ] ❌ Failed to download attachment from MinIO: %v", err)
				span.RecordError(err)
				// Requeue message on temporary storage failures
				_ = d.Nack(false, true)
				return
			}
		}

		emailCtx, cancel := context.WithTimeout(ctx, 15*time.Second)
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
