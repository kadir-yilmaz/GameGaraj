package logging

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"strings"
	"time"
)

// Setup redirects Go standard logger to both stdout and a fixed Elasticsearch index (gamegaraj-{service}-logs).
func Setup(ctx context.Context, serviceName string) {
	esURI := strings.TrimRight(os.Getenv("ElasticSearchSettings__Uri"), "/")
	if esURI == "" || esURI == "disabled" {
		return
	}

	w := newElasticWriter(ctx, esURI, fmt.Sprintf("gamegaraj-%s-logs", serviceName), serviceName)
	log.SetOutput(io.MultiWriter(os.Stdout, w))
}

type elasticWriter struct {
	bulkURL string
	action  string
	service string
	ch      chan string
	client  *http.Client
}

func newElasticWriter(ctx context.Context, esURI, indexName, service string) *elasticWriter {
	actionBytes, _ := json.Marshal(map[string]any{"index": map[string]string{"_index": indexName}})
	w := &elasticWriter{
		bulkURL: esURI + "/_bulk",
		action:  string(actionBytes),
		service: service,
		ch:      make(chan string, 1024),
		client:  &http.Client{Timeout: 5 * time.Second},
	}
	go w.flushLoop(ctx)
	return w
}

func (w *elasticWriter) Write(p []byte) (int, error) {
	msg := strings.TrimSpace(string(p))
	if msg != "" {
		doc, _ := json.Marshal(map[string]any{
			"@timestamp": time.Now().UTC().Format(time.RFC3339Nano),
			"service":    w.service,
			"level":      "Information",
			"message":    msg,
		})
		select {
		case w.ch <- string(doc):
		default: // drop oldest/overflow silently
		}
	}
	return len(p), nil
}

func (w *elasticWriter) flushLoop(ctx context.Context) {
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

func (w *elasticWriter) send(batch []string) {
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
