package service

import (
	"bytes"
	"context"
	"fmt"
	"log"
	"net/smtp"

	"gamegaraj-notification-api/internal/config"

	"github.com/jordan-wright/email"
)

type EmailService interface {
	SendEmail(ctx context.Context, to, subject, htmlBody string, attachmentBytes []byte, attachmentName string) error
}

type smtpEmailService struct {
	host     string
	port     int
	username string
	password string
	from     string
	fromName string
}

func NewEmailService(cfg *config.Config) EmailService {
	return &smtpEmailService{
		host:     cfg.SmtpHost,
		port:     cfg.SmtpPort,
		username: cfg.SmtpUsername,
		password: cfg.SmtpPassword,
		from:     cfg.SmtpFromEmail,
		fromName: cfg.SmtpFromName,
	}
}

func (s *smtpEmailService) SendEmail(ctx context.Context, to, subject, htmlBody string, attachmentBytes []byte, attachmentName string) error {
	log.Printf("[EmailService] Sending email to: %s with subject: %s", to, subject)

	e := email.NewEmail()
	e.From = fmt.Sprintf("%s <%s>", s.fromName, s.from)
	e.To = []string{to}
	e.Subject = subject
	e.HTML = []byte(htmlBody)

	// Attach file if bytes are provided
	if len(attachmentBytes) > 0 && attachmentName != "" {
		log.Printf("[EmailService] Attaching file: %s (%d bytes)", attachmentName, len(attachmentBytes))
		_, err := e.Attach(bytes.NewReader(attachmentBytes), attachmentName, "application/pdf")
		if err != nil {
			return fmt.Errorf("failed to attach file: %w", err)
		}
	}

	addr := fmt.Sprintf("%s:%d", s.host, s.port)
	auth := smtp.PlainAuth("", s.username, s.password, s.host)

	// Send e-mail
	err := e.Send(addr, auth)
	if err != nil {
		return fmt.Errorf("failed to send email via SMTP: %w", err)
	}

	log.Printf("[EmailService] Email successfully sent to: %s", to)
	return nil
}
