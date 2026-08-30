package config

import (
	"bufio"
	"os"
	"path/filepath"
	"strconv"
	"strings"
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
	RedisURL        string
}

func loadDotEnv() {
	// Look for .env in current, parent, or project root directory
	paths := []string{".env", "../.env", "../../.env"}
	for _, p := range paths {
		if abs, err := filepath.Abs(p); err == nil {
			if file, err := os.Open(abs); err == nil {
				defer file.Close()
				scanner := bufio.NewScanner(file)
				for scanner.Scan() {
					line := strings.TrimSpace(scanner.Text())
					if line == "" || strings.HasPrefix(line, "#") {
						continue
					}
					parts := strings.SplitN(line, "=", 2)
					if len(parts) == 2 {
						key := strings.TrimSpace(parts[0])
						val := strings.Trim(strings.TrimSpace(parts[1]), "\"'")
						if _, exists := os.LookupEnv(key); !exists {
							os.Setenv(key, val)
						}
					}
				}
				break
			}
		}
	}
}

func LoadConfig() *Config {
	loadDotEnv()

	return &Config{
		Port:            getEnv("PORT", "5025"),
		RabbitMQURL:     getEnv("RabbitMQUrl", "localhost"),
		MinioEndpoint:   getEnv("Minio__Endpoint", "http://minio.kadiryilmaz.online"),
		MinioAccessKey:  getEnv("Minio__AccessKey", ""),
		MinioSecretKey:  getEnv("Minio__SecretKey", ""),
		MinioBucket:     getEnv("Minio__BucketName", "gamegaraj"),
		MinioSecure:     getEnvBool("Minio__Secure", false),
		SmtpHost:        getEnv("SMTP_HOST", "smtp.gmail.com"),
		SmtpPort:        getEnvInt("SMTP_PORT", 587),
		SmtpUsername:    getEnv("SMTP_USERNAME", getEnv("SMTP_USER", getEnv("EmailSettings__SmtpUsername", ""))),
		SmtpPassword:    getEnv("SMTP_PASSWORD", getEnv("EmailSettings__SmtpPassword", "")),
		SmtpFromEmail:   getEnv("SMTP_FROM_EMAIL", getEnv("SMTP_USER", getEnv("EmailSettings__SmtpUsername", ""))),
		SmtpFromName:    getEnv("SMTP_FROM_NAME", "GameGaraj"),
		OtlpEndpoint:    getEnv("OpenTelemetry__OtlpEndpoint", "http://localhost:4317"),
		Environment:     getEnv("ENVIRONMENT", "Development"),
		RedisURL:        getEnv("Redis", getEnv("RedisUrl", "localhost:6380")),
	}
}

func getEnv(key, defaultVal string) string {
	if value, exists := os.LookupEnv(key); exists && value != "" {
		return value
	}
	return defaultVal
}

func getEnvInt(key string, defaultVal int) int {
	if value, exists := os.LookupEnv(key); exists && value != "" {
		if val, err := strconv.Atoi(value); err == nil {
			return val
		}
	}
	return defaultVal
}

func getEnvBool(key string, defaultVal bool) bool {
	if value, exists := os.LookupEnv(key); exists && value != "" {
		if val, err := strconv.ParseBool(value); err == nil {
			return val
		}
	}
	return defaultVal
}
