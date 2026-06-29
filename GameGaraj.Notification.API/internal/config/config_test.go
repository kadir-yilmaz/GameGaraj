package config

import (
	"os"
	"testing"
)

func TestLoadConfig(t *testing.T) {
	// Set mock environment variables
	os.Setenv("PORT", "9090")
	os.Setenv("RabbitMQUrl", "rabbitmq-host")
	os.Setenv("Minio__BucketName", "test-bucket")
	os.Setenv("Minio__Secure", "true")
	os.Setenv("SMTP_PORT", "465")

	cfg := LoadConfig()

	// Assertions
	if cfg.Port != "9090" {
		t.Errorf("Expected Port to be 9090, got %s", cfg.Port)
	}
	if cfg.RabbitMQURL != "rabbitmq-host" {
		t.Errorf("Expected RabbitMQURL to be rabbitmq-host, got %s", cfg.RabbitMQURL)
	}
	if cfg.MinioBucket != "test-bucket" {
		t.Errorf("Expected MinioBucket to be test-bucket, got %s", cfg.MinioBucket)
	}
	if !cfg.MinioSecure {
		t.Errorf("Expected MinioSecure to be true, got %t", cfg.MinioSecure)
	}
	if cfg.SmtpPort != 465 {
		t.Errorf("Expected SmtpPort to be 465, got %d", cfg.SmtpPort)
	}

	// Clean up env
	os.Unsetenv("PORT")
	os.Unsetenv("RabbitMQUrl")
	os.Unsetenv("Minio__BucketName")
	os.Unsetenv("Minio__Secure")
	os.Unsetenv("SMTP_PORT")
}
