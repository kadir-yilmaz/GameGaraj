package consumer

import (
	"context"
	"encoding/json"
	"fmt"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
	"go.uber.org/zap"

	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/model"
	"github.com/kadir-yilmaz/gamegaraj-search-api/internal/service"
)

const (
	// Queue names for consuming product events
	// These must match the MassTransit publish conventions used by Catalog API
	productCreatedQueue = "product-created-search-service"
	productUpdatedQueue = "product-updated-search-service"
	productDeletedQueue = "product-deleted-search-service"

	// Exchange names — MassTransit uses type-based exchange names
	productCreatedExchange = "GameGaraj.Shared.Events:ProductCreatedForSearch"
	productUpdatedExchange = "GameGaraj.Shared.Events:ProductUpdatedForSearch"
	productDeletedExchange = "GameGaraj.Shared.Events:ProductDeletedForSearch"
)

// RabbitMQConsumer consumes product events from RabbitMQ and syncs to Elasticsearch
type RabbitMQConsumer struct {
	conn    *amqp.Connection
	channel *amqp.Channel
	service *service.SearchService
	logger  *zap.Logger
	url     string
}

// NewRabbitMQConsumer creates a new RabbitMQ consumer
func NewRabbitMQConsumer(url, username, password string, svc *service.SearchService, logger *zap.Logger) *RabbitMQConsumer {
	amqpURL := fmt.Sprintf("amqp://%s:%s@%s/", username, password, url)
	return &RabbitMQConsumer{
		service: svc,
		logger:  logger,
		url:     amqpURL,
	}
}

// Start connects to RabbitMQ and starts consuming messages in goroutines.
// It blocks until the context is cancelled.
func (c *RabbitMQConsumer) Start(ctx context.Context) error {
	var err error

	// Connection retry loop
	for {
		c.conn, err = amqp.Dial(c.url)
		if err == nil {
			break
		}
		c.logger.Warn("Failed to connect to RabbitMQ, retrying in 5s...", zap.Error(err))
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(5 * time.Second):
		}
	}

	c.channel, err = c.conn.Channel()
	if err != nil {
		return fmt.Errorf("failed to open channel: %w", err)
	}

	// Set prefetch count for fair dispatch
	if err := c.channel.Qos(10, 0, false); err != nil {
		return fmt.Errorf("failed to set QoS: %w", err)
	}

	c.logger.Info("Connected to RabbitMQ, setting up consumers...")

	// Setup each consumer in its own goroutine
	if err := c.setupConsumer(ctx, productCreatedExchange, productCreatedQueue, c.handleProductCreated); err != nil {
		return err
	}
	if err := c.setupConsumer(ctx, productUpdatedExchange, productUpdatedQueue, c.handleProductUpdated); err != nil {
		return err
	}
	if err := c.setupConsumer(ctx, productDeletedExchange, productDeletedQueue, c.handleProductDeleted); err != nil {
		return err
	}

	c.logger.Info("All RabbitMQ consumers started")

	// Wait for context cancellation
	<-ctx.Done()
	return c.Close()
}

func (c *RabbitMQConsumer) setupConsumer(ctx context.Context, exchangeName, queueName string, handler func(context.Context, amqp.Delivery)) error {
	// Declare fanout exchange (MassTransit style)
	err := c.channel.ExchangeDeclare(
		exchangeName,
		"fanout",
		true,  // durable
		false, // auto-deleted
		false, // internal
		false, // no-wait
		nil,
	)
	if err != nil {
		return fmt.Errorf("failed to declare exchange %s: %w", exchangeName, err)
	}

	// Declare queue
	q, err := c.channel.QueueDeclare(
		queueName,
		true,  // durable
		false, // auto-delete
		false, // exclusive
		false, // no-wait
		nil,
	)
	if err != nil {
		return fmt.Errorf("failed to declare queue %s: %w", queueName, err)
	}

	// Bind queue to exchange
	err = c.channel.QueueBind(
		q.Name,
		"",           // routing key (fanout ignores this)
		exchangeName,
		false,
		nil,
	)
	if err != nil {
		return fmt.Errorf("failed to bind queue %s to exchange %s: %w", queueName, exchangeName, err)
	}

	// Start consuming
	deliveries, err := c.channel.Consume(
		q.Name,
		"",    // consumer tag
		false, // auto-ack (manual ack for reliability)
		false, // exclusive
		false, // no-local
		false, // no-wait
		nil,
	)
	if err != nil {
		return fmt.Errorf("failed to start consuming %s: %w", queueName, err)
	}

	// Process messages in a goroutine
	go func() {
		for {
			select {
			case <-ctx.Done():
				return
			case msg, ok := <-deliveries:
				if !ok {
					c.logger.Warn("Consumer channel closed", zap.String("queue", queueName))
					return
				}
				handler(ctx, msg)
			}
		}
	}()

	c.logger.Info("Consumer started", zap.String("queue", queueName), zap.String("exchange", exchangeName))
	return nil
}

// MassTransit wraps the message body in an envelope with a "message" field
type massTransitEnvelope struct {
	Message json.RawMessage `json:"message"`
}

func (c *RabbitMQConsumer) handleProductCreated(ctx context.Context, msg amqp.Delivery) {
	var event model.ProductEvent
	if err := c.unmarshalMessage(msg.Body, &event); err != nil {
		c.logger.Error("Failed to unmarshal ProductCreated event", zap.Error(err))
		msg.Nack(false, false) // don't requeue malformed messages
		return
	}

	c.logger.Info("ProductCreated event received",
		zap.String("productId", event.ID),
		zap.String("name", event.Name))

	if err := c.service.IndexProduct(ctx, &event); err != nil {
		c.logger.Error("Failed to index product from ProductCreated event",
			zap.String("productId", event.ID),
			zap.Error(err))
		msg.Nack(false, true) // requeue for retry
		return
	}

	msg.Ack(false)
}

func (c *RabbitMQConsumer) handleProductUpdated(ctx context.Context, msg amqp.Delivery) {
	var event model.ProductEvent
	if err := c.unmarshalMessage(msg.Body, &event); err != nil {
		c.logger.Error("Failed to unmarshal ProductUpdated event", zap.Error(err))
		msg.Nack(false, false)
		return
	}

	c.logger.Info("ProductUpdated event received",
		zap.String("productId", event.ID),
		zap.String("name", event.Name))

	if err := c.service.IndexProduct(ctx, &event); err != nil {
		c.logger.Error("Failed to index product from ProductUpdated event",
			zap.String("productId", event.ID),
			zap.Error(err))
		msg.Nack(false, true)
		return
	}

	msg.Ack(false)
}

func (c *RabbitMQConsumer) handleProductDeleted(ctx context.Context, msg amqp.Delivery) {
	var deleteEvent struct {
		ID string `json:"id"`
	}
	if err := c.unmarshalMessage(msg.Body, &deleteEvent); err != nil {
		c.logger.Error("Failed to unmarshal ProductDeleted event", zap.Error(err))
		msg.Nack(false, false)
		return
	}

	c.logger.Info("ProductDeleted event received", zap.String("productId", deleteEvent.ID))

	if err := c.service.DeleteProduct(ctx, deleteEvent.ID); err != nil {
		c.logger.Error("Failed to delete product from ProductDeleted event",
			zap.String("productId", deleteEvent.ID),
			zap.Error(err))
		msg.Nack(false, true)
		return
	}

	msg.Ack(false)
}

// unmarshalMessage handles both MassTransit envelope format and plain JSON
func (c *RabbitMQConsumer) unmarshalMessage(body []byte, target interface{}) error {
	// Try MassTransit envelope first
	var envelope massTransitEnvelope
	if err := json.Unmarshal(body, &envelope); err == nil && len(envelope.Message) > 0 {
		return json.Unmarshal(envelope.Message, target)
	}

	// Fallback to plain JSON
	return json.Unmarshal(body, target)
}

// Close closes the RabbitMQ connection
func (c *RabbitMQConsumer) Close() error {
	if c.channel != nil {
		c.channel.Close()
	}
	if c.conn != nil {
		return c.conn.Close()
	}
	return nil
}
