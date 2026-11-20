from __future__ import annotations

import urllib.parse
import urllib.request

from app.db.repositories import SettingsRepository


class NtfyNotifier:
    """Sends urgent (priority=5) mobile alarm notifications via ntfy."""

    channel = "ntfy"

    def __init__(self, app_settings: SettingsRepository) -> None:
        self.app_settings = app_settings

    def _server_url(self) -> str:
        cfg = self.app_settings.get_all()
        return (cfg.get("ntfy_server_url") or "https://ntfy.sh").strip().rstrip("/")

    def is_configured(self) -> bool:
        return bool(self._server_url())

    def send(self, recipient: str, subject: str, body: str) -> tuple[bool, str]:
        topic = (recipient or "").strip().lstrip("/")
        if not topic:
            return False, "Kullanıcının ntfy topic alanı boş"
        url = f"{self._server_url()}/{urllib.parse.quote(topic)}"
        title = (subject or "GameGaraj Kritik Alarm").encode("ascii", errors="ignore").decode("ascii") or "GameGaraj Alarm"
        req = urllib.request.Request(
            url,
            data=body.encode("utf-8"),
            method="POST",
            headers={
                "Title": title,
                "Priority": "urgent",
                "Tags": "rotating_light",
                "Content-Type": "text/plain; charset=utf-8",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=15):
                return True, ""
        except Exception as exc:
            return False, str(exc)
