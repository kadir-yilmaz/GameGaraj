from __future__ import annotations

import smtplib
from email.message import EmailMessage

from app.config import Settings
from app.db.repositories import SettingsRepository


class EmailNotifier:
    channel = "email"

    def __init__(self, settings: Settings, app_settings: SettingsRepository | None = None) -> None:
        self.settings = settings
        self.app_settings = app_settings

    def _smtp_config(self) -> tuple[str, int, str, str, str, bool]:
        cfg = self.app_settings.get_all() if self.app_settings else {}
        host = (cfg.get("smtp_host") or self.settings.smtp_host).strip()
        try:
            port = int(cfg.get("smtp_port") or self.settings.smtp_port)
        except ValueError:
            port = self.settings.smtp_port
        user = (cfg.get("smtp_user") or self.settings.smtp_user).strip()
        password = cfg.get("smtp_pass") or self.settings.smtp_pass
        sender = (cfg.get("smtp_from") or self.settings.smtp_from or user or "noreply@gamegaraj.local").strip()
        tls = (cfg.get("smtp_tls") or ("true" if self.settings.smtp_tls else "false")).lower() in {"1", "true", "yes"}
        return host, port, user, password, sender, tls

    def is_configured(self) -> bool:
        host, *_ = self._smtp_config()
        return bool(host)

    def send(self, recipient: str, subject: str, body: str) -> tuple[bool, str]:
        host, port, user, password, sender, tls = self._smtp_config()
        if not host:
            return False, "SMTP sunucusu yapılandırılmamış (SMTP Host boş)"
        if not recipient:
            return False, "Alıcı e-posta adresi boş"

        msg = EmailMessage()
        msg["Subject"] = subject
        msg["From"] = sender
        msg["To"] = recipient
        msg.set_content(body)

        try:
            if port == 465:
                with smtplib.SMTP_SSL(host, port, timeout=15) as server:
                    if user:
                        server.login(user, password)
                    server.send_message(msg)
            else:
                with smtplib.SMTP(host, port, timeout=15) as server:
                    if tls:
                        server.starttls()
                    if user:
                        server.login(user, password)
                    server.send_message(msg)
            return True, ""
        except Exception as exc:
            return False, str(exc)
