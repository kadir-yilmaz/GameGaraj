FROM golang:alpine AS build

ENV GOTOOLCHAIN=auto
WORKDIR /app

# Download dependencies first (layer caching)
COPY go.mod go.sum ./
RUN go mod download

# Copy source code and build
COPY . .
RUN go mod tidy && CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o /search-api ./cmd/server

# Final minimal image
FROM alpine:3.19

RUN apk --no-cache add ca-certificates

COPY --from=build /search-api /search-api
COPY --from=build /app/config.yaml /config.yaml

EXPOSE 8080

CMD ["/search-api"]
