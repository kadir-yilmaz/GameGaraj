from __future__ import annotations

import smtplib
from email.message import EmailMessage

from app.config import Settings


class EmailNotifier:
    channel = "email"

    def __init__(self, settings: Settings) -> None:
        self.settings = settings

    def is_configured(self) -> bool:
        return bool(self.settings.smtp_host)

    def send(self, recipient: str, subject: str, body: str) -> tuple[bool, str]:
        if not self.is_configured():
            return False, "SMTP sunucusu yapılandırılmamış (SMTP_HOST boş)"
        if not recipient:
            return False, "Alıcı e-posta adresi boş"

        msg = EmailMessage()
        msg["Subject"] = subject
        msg["From"] = self.settings.smtp_from or self.settings.smtp_user or "noreply@gamegaraj.local"
        msg["To"] = recipient
        msg.set_content(body)

        try:
            if self.settings.smtp_port == 465:
                with smtplib.SMTP_SSL(self.settings.smtp_host, self.settings.smtp_port, timeout=15) as server:
                    if self.settings.smtp_user:
                        server.login(self.settings.smtp_user, self.settings.smtp_pass)
                    server.send_message(msg)
            else:
                with smtplib.SMTP(self.settings.smtp_host, self.settings.smtp_port, timeout=15) as server:
                    if self.settings.smtp_tls:
                        server.starttls()
                    if self.settings.smtp_user:
                        server.login(self.settings.smtp_user, self.settings.smtp_pass)
                    server.send_message(msg)
            return True, ""
        except Exception as exc:
            return False, str(exc)
