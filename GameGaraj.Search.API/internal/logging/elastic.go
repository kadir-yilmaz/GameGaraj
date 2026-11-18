package logging

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"strings"
	"time"
)

// NewElasticSyncer returns a zapcore.WriteSyncer-compatible writer for gamegaraj-{service}-logs.
func NewElasticSyncer(ctx context.Context, serviceName string) *ElasticWriter {
	esURI := strings.TrimRight(os.Getenv("ElasticSearchSettings__Uri"), "/")
	if esURI == "" {
		esURI = strings.TrimRight(os.Getenv("SEARCH_ELASTICSEARCH_URI"), "/")
	}
	if esURI == "" || esURI == "disabled" {
		return nil
	}

	actionBytes, _ := json.Marshal(map[string]any{"index": map[string]string{"_index": fmt.Sprintf("gamegaraj-%s-logs", serviceName)}})
	w := &ElasticWriter{
		bulkURL: esURI + "/_bulk",
		action:  string(actionBytes),
		ch:      make(chan string, 1024),
		client:  &http.Client{Timeout: 5 * time.Second},
	}
	go w.flushLoop(ctx)
	return w
}

type ElasticWriter struct {
	bulkURL string
	action  string
	ch      chan string
	client  *http.Client
}

func (w *ElasticWriter) Write(p []byte) (int, error) {
	line := strings.TrimSpace(string(p))
	if line != "" {
		select {
		case w.ch <- line:
		default:
		}
	}
	return len(p), nil
}

func (w *ElasticWriter) Sync() error { return nil }

func (w *ElasticWriter) flushLoop(ctx context.Context) {
	ticker := time.NewTicker(2 * time.Second)
	defer ticker.Stop()

	var batch []string
	for {
		select {
		case <-ctx.Done():
			w.send(batch)
			return
		case line := <-w.ch:
			batch = append(batch, line)
			if len(batch) >= 50 {
				w.send(batch)
				batch = nil
			}
		case <-ticker.C:
			if len(batch) > 0 {
				w.send(batch)
				batch = nil
			}
		}
	}
}

func (w *ElasticWriter) send(batch []string) {
	if len(batch) == 0 {
		return
	}
	var buf bytes.Buffer
	for _, doc := range batch {
		buf.WriteString(w.action)
		buf.WriteByte('\n')
		buf.WriteString(doc)
		buf.WriteByte('\n')
	}
	req, err := http.NewRequest(http.MethodPost, w.bulkURL, &buf)
	if err != nil {
		return
	}
	req.Header.Set("Content-Type", "application/x-ndjson")
	resp, err := w.client.Do(req)
	if err == nil {
		resp.Body.Close()
	}
}
