# Stage 1: Build the Go binary
FROM golang:1.26-alpine AS builder

WORKDIR /app

# Copy dependency files
COPY go.mod go.sum ./
RUN go mod download

# Copy source code
COPY . .

# Build static binary
RUN CGO_ENABLED=0 GOOS=linux go build -ldflags="-w -s" -o /app/notification-api cmd/api/main.go

# Stage 2: Minimal runtime image
FROM alpine:3.19

WORKDIR /app

# Install ca-certificates (needed for secure SMTP connection)
RUN apk --no-cache add ca-certificates

# Copy binary from builder
COPY --from=builder /app/notification-api .

# Expose container port
EXPOSE 8080

# Run the app
ENTRYPOINT ["./notification-api"]
