package service

import (
	"context"
	"log"
)

type SmsService interface {
	SendSms(ctx context.Context, to, message string) error
}

type mockSmsService struct{}

func NewSmsService() SmsService {
	return &mockSmsService{}
}

func (s *mockSmsService) SendSms(ctx context.Context, to, message string) error {
	log.Printf("[SmsService] (SIMULATION) Sending SMS to: %s", to)
	log.Printf("[SmsService] (SIMULATION) Message Content: %s", message)
	log.Printf("[SmsService] (SIMULATION) SMS sent successfully")
	return nil
}
