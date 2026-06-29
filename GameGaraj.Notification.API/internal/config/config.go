package config

import (
	"os"
	"strconv"
)

type Config struct {
	Port            string
	RabbitMQURL     string
	MinioEndpoint   string
	MinioAccessKey  string
	MinioSecretKey  string
	MinioBucket     string
	MinioSecure     bool
	SmtpHost        string
	SmtpPort        int
	SmtpUsername    string
	SmtpPassword    string
	SmtpFromEmail   string
	SmtpFromName    string
	OtlpEndpoint    string
	Environment     string
}

func LoadConfig() *Config {
	return &Config{
		Port:            getEnv("PORT", "8080"),
		RabbitMQURL:     getEnv("RabbitMQUrl", "localhost"), // matches C# naming or standard ENV
		MinioEndpoint:   getEnv("Minio__Endpoint", "http://minio.kadiryilmaz.online"),
		MinioAccessKey:  getEnv("Minio__AccessKey", ""),
		MinioSecretKey:  getEnv("Minio__SecretKey", ""),
		MinioBucket:     getEnv("Minio__BucketName", "gamegaraj"),
		MinioSecure:     getEnvBool("Minio__Secure", false),
		SmtpHost:        getEnv("SMTP_HOST", "smtp.gmail.com"),
		SmtpPort:        getEnvInt("SMTP_PORT", 587),
		SmtpUsername:    getEnv("SMTP_USERNAME", "kadiryilmaz19821@gmail.com"),
		SmtpPassword:    getEnv("SMTP_PASSWORD", "jhvlcbpxeepdjxmf"),
		SmtpFromEmail:   getEnv("SMTP_FROM_EMAIL", "kadiryilmaz19821@gmail.com"),
		SmtpFromName:    getEnv("SMTP_FROM_NAME", "GameGaraj"),
		OtlpEndpoint:    getEnv("OpenTelemetry__OtlpEndpoint", "http://localhost:4317"),
		Environment:     getEnv("ENVIRONMENT", "Development"),
	}
}

func getEnv(key, defaultVal string) string {
	if value, exists := os.LookupEnv(key); exists {
		return value
	}
	return defaultVal
}

func getEnvInt(key string, defaultVal int) int {
	if value, exists := os.LookupEnv(key); exists {
		if val, err := strconv.Atoi(value); err == nil {
			return val
		}
	}
	return defaultVal
}

func getEnvBool(key string, defaultVal bool) bool {
	if value, exists := os.LookupEnv(key); exists {
		if val, err := strconv.ParseBool(value); err == nil {
			return val
		}
	}
	return defaultVal
}
