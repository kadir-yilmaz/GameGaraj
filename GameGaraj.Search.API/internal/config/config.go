package config

import (
	"log"
	"strings"

	"github.com/spf13/viper"
)

// Config holds all application configuration
type Config struct {
	Server        ServerConfig        `mapstructure:"server"`
	Catalog       CatalogConfig       `mapstructure:"catalog"`
	Elasticsearch ElasticsearchConfig `mapstructure:"elasticsearch"`
	RabbitMQ      RabbitMQConfig      `mapstructure:"rabbitmq"`
	Redis         RedisConfig         `mapstructure:"redis"`
}

type ServerConfig struct {
	Port string `mapstructure:"port"`
}

type CatalogConfig struct {
	URL string `mapstructure:"url"`
}

type ElasticsearchConfig struct {
	URI          string `mapstructure:"uri"`
	DefaultIndex string `mapstructure:"default_index"`
}

type RabbitMQConfig struct {
	URL      string `mapstructure:"url"`
	Username string `mapstructure:"username"`
	Password string `mapstructure:"password"`
}

type RedisConfig struct {
	Address      string `mapstructure:"address"`
	Password     string `mapstructure:"password"`
	DB           int    `mapstructure:"db"`
	InstanceName string `mapstructure:"instance_name"`
}

// Load reads configuration from file and environment variables
func Load() *Config {
	viper.SetConfigName("config")
	viper.SetConfigType("yaml")
	viper.AddConfigPath(".")
	viper.AddConfigPath("./config")

	// Defaults
	viper.SetDefault("server.port", ":5082")
	viper.SetDefault("catalog.url", "http://localhost:5011")
	viper.SetDefault("elasticsearch.uri", "http://localhost:9201")
	viper.SetDefault("elasticsearch.default_index", "products")
	viper.SetDefault("rabbitmq.url", "localhost:5672")
	viper.SetDefault("rabbitmq.username", "guest")
	viper.SetDefault("rabbitmq.password", "guest")
	viper.SetDefault("redis.address", "localhost:6380")
	viper.SetDefault("redis.password", "")
	viper.SetDefault("redis.db", 0)
	viper.SetDefault("redis.instance_name", "search-cache:")

	// Environment variable overrides (SEARCH_SERVER_PORT, SEARCH_CATALOG_URL, etc.)
	viper.SetEnvPrefix("SEARCH")
	viper.SetEnvKeyReplacer(strings.NewReplacer(".", "_"))
	viper.AutomaticEnv()

	if err := viper.ReadInConfig(); err != nil {
		log.Printf("[Config] No config file found, using defaults and env vars: %v", err)
	}

	var cfg Config
	if err := viper.Unmarshal(&cfg); err != nil {
		log.Fatalf("[Config] Failed to unmarshal config: %v", err)
	}

	return &cfg
}
