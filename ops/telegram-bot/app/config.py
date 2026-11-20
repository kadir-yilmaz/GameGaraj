from __future__ import annotations

import os
from dataclasses import dataclass
from datetime import timedelta, timezone


@dataclass(frozen=True)
class Settings:
    telegram_token_file: str
    telegram_chat_id: str
    gamegaraj_namespace: str
    argocd_namespace: str
    tz_offset_hours: float
    node_check_interval: int
    node_debounce_checks: int
    db_path: str
    default_report_interval_hours: int
    default_night_start: str
    default_night_end: str
    smtp_host: str
    smtp_port: int
    smtp_user: str
    smtp_pass: str
    smtp_from: str
    smtp_tls: bool
    web_auth_password: str

    @property
    def tz(self) -> timezone:
        return timezone(timedelta(hours=self.tz_offset_hours))

    def read_bot_token(self) -> str:
        env_token = os.environ.get("TELEGRAM_BOT_TOKEN", "").strip()
        if env_token:
            return env_token
        if os.path.exists(self.telegram_token_file):
            with open(self.telegram_token_file, encoding="utf-8") as fh:
                return fh.read().strip()
        return ""

    @classmethod
    def from_env(cls) -> "Settings":
        return cls(
            telegram_token_file=os.environ.get("TELEGRAM_TOKEN_FILE", "/telegram/token"),
            telegram_chat_id=os.environ.get("TELEGRAM_CHAT_ID", "").strip(),
            gamegaraj_namespace=os.environ.get("GAMEGARAJ_NAMESPACE", "gamegaraj"),
            argocd_namespace=os.environ.get("ARGOCD_NAMESPACE", "argocd"),
            tz_offset_hours=float(os.environ.get("TZ_OFFSET_HOURS", "3")),
            node_check_interval=max(5, int(os.environ.get("NODE_CHECK_INTERVAL", "30"))),
            node_debounce_checks=max(1, int(os.environ.get("NODE_DEBOUNCE_CHECKS", "2"))),
            db_path=os.environ.get("DB_PATH", "/data/bot.db"),
            default_report_interval_hours=int(os.environ.get("DEFAULT_REPORT_INTERVAL_HOURS", "6")),
            default_night_start=os.environ.get("DEFAULT_NIGHT_START", "22:00"),
            default_night_end=os.environ.get("DEFAULT_NIGHT_END", "08:00"),
            smtp_host=os.environ.get("SMTP_HOST", "").strip(),
            smtp_port=int(os.environ.get("SMTP_PORT", "587")),
            smtp_user=os.environ.get("SMTP_USER", "").strip(),
            smtp_pass=os.environ.get("SMTP_PASS", ""),
            smtp_from=os.environ.get("SMTP_FROM", "").strip(),
            smtp_tls=os.environ.get("SMTP_TLS", "true").lower() in {"1", "true", "yes"},
            web_auth_password=os.environ.get("WEB_AUTH_PASSWORD", "").strip(),
        )
