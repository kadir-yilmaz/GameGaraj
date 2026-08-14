package cache

import (
	"context"
	"time"

	"github.com/redis/go-redis/v9"
	"go.uber.org/zap"
)

// Service provides Redis cache operations with a key prefix for isolation.
// Catalog API uses "catalog-cache:" prefix, Search API uses "search-cache:" prefix.
type Service struct {
	client *redis.Client
	prefix string
	logger *zap.Logger
}

// NewService creates a new Redis cache service
func NewService(address, password string, db int, prefix string, logger *zap.Logger) *Service {
	client := redis.NewClient(&redis.Options{
		Addr:     address,
		Password: password,
		DB:       db,
	})

	return &Service{
		client: client,
		prefix: prefix,
		logger: logger,
	}
}

// Ping checks if Redis is reachable
func (s *Service) Ping(ctx context.Context) error {
	return s.client.Ping(ctx).Err()
}

// Close closes the Redis connection
func (s *Service) Close() error {
	return s.client.Close()
}

// fullKey prepends the instance prefix to the key
func (s *Service) fullKey(key string) string {
	return s.prefix + key
}

// Get retrieves a value from cache. Returns empty string and false if key doesn't exist.
func (s *Service) Get(ctx context.Context, key string) (string, bool) {
	val, err := s.client.Get(ctx, s.fullKey(key)).Result()
	if err == redis.Nil {
		return "", false
	}
	if err != nil {
		s.logger.Warn("Redis GET failed", zap.String("key", key), zap.Error(err))
		return "", false
	}
	return val, true
}

// Set stores a value in cache with the given TTL
func (s *Service) Set(ctx context.Context, key string, value string, ttl time.Duration) {
	err := s.client.Set(ctx, s.fullKey(key), value, ttl).Err()
	if err != nil {
		s.logger.Warn("Redis SET failed", zap.String("key", key), zap.Error(err))
	}
}

// Delete removes one or more keys from cache
func (s *Service) Delete(ctx context.Context, keys ...string) {
	fullKeys := make([]string, len(keys))
	for i, k := range keys {
		fullKeys[i] = s.fullKey(k)
	}
	err := s.client.Del(ctx, fullKeys...).Err()
	if err != nil {
		s.logger.Warn("Redis DEL failed", zap.Strings("keys", keys), zap.Error(err))
	}
}

// DeletePattern removes all keys matching a pattern using SCAN (safe for production).
// Pattern example: "query_*" will delete all keys like "search-cache:query_ps5", "search-cache:query_oyun" etc.
func (s *Service) DeletePattern(ctx context.Context, pattern string) {
	fullPattern := s.fullKey(pattern)
	var cursor uint64
	var deleted int64

	for {
		keys, nextCursor, err := s.client.Scan(ctx, cursor, fullPattern, 100).Result()
		if err != nil {
			s.logger.Warn("Redis SCAN failed", zap.String("pattern", pattern), zap.Error(err))
			return
		}

		if len(keys) > 0 {
			pipe := s.client.Pipeline()
			for _, key := range keys {
				pipe.Del(ctx, key)
			}
			cmds, err := pipe.Exec(ctx)
			if err != nil {
				s.logger.Warn("Redis pipeline DEL failed", zap.String("pattern", pattern), zap.Error(err))
			} else {
				for _, cmd := range cmds {
					if delCmd, ok := cmd.(*redis.IntCmd); ok {
						deleted += delCmd.Val()
					}
				}
			}
		}

		cursor = nextCursor
		if cursor == 0 {
			break
		}
	}

	if deleted > 0 {
		s.logger.Info("Redis pattern delete completed",
			zap.String("pattern", pattern),
			zap.Int64("deletedKeys", deleted))
	}
}
