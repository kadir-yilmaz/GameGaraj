package storage

import (
	"context"
	"fmt"
	"io"
	"log"
	"net/url"
	"strings"

	"gamegaraj-notification-api/internal/config"

	"github.com/minio/minio-go/v7"
	"github.com/minio/minio-go/v7/pkg/credentials"
)

type MinioClient struct {
	client     *minio.Client
	bucketName string
}

func NewMinioClient(cfg *config.Config) (*MinioClient, error) {
	endpoint := cfg.MinioEndpoint

	// Clean endpoint if it contains scheme prefix (Minio Go client wants host:port or host only)
	if u, err := url.Parse(endpoint); err == nil && u.Host != "" {
		endpoint = u.Host
	}

	log.Printf("[MinIO] Initializing MinIO Client with endpoint: %s, bucket: %s, secure: %t", endpoint, cfg.MinioBucket, cfg.MinioSecure)

	client, err := minio.New(endpoint, &minio.Options{
		Creds:  credentials.NewStaticV4(cfg.MinioAccessKey, cfg.MinioSecretKey, ""),
		Secure: cfg.MinioSecure,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to create MinIO client: %w", err)
	}

	return &MinioClient{
		client:     client,
		bucketName: cfg.MinioBucket,
	}, nil
}

func (m *MinioClient) DownloadFile(ctx context.Context, objectName string) ([]byte, error) {
	// Clean objectName prefix if it contains bucket name (e.g. "gamegaraj/invoices/..." -> "invoices/...")
	objectName = strings.TrimPrefix(objectName, m.bucketName+"/")

	log.Printf("[MinIO] Downloading object: %s from bucket: %s", objectName, m.bucketName)

	object, err := m.client.GetObject(ctx, m.bucketName, objectName, minio.GetObjectOptions{})
	if err != nil {
		return nil, fmt.Errorf("failed to get object: %w", err)
	}
	defer object.Close()

	// Read content
	data, err := io.ReadAll(object)
	if err != nil {
		// Checking if bucket exists or connection error
		return nil, fmt.Errorf("failed to read object data: %w", err)
	}

	log.Printf("[MinIO] Successfully downloaded object: %s, size: %d bytes", objectName, len(data))
	return data, nil
}
