from __future__ import annotations

import json
import urllib.request

from app.db.repositories import SettingsRepository


class NtfyNotifier:
    """Sends mobile notifications via ntfy JSON API (full UTF-8 Turkish support)."""

    channel = "ntfy"

    def __init__(self, app_settings: SettingsRepository) -> None:
        self.app_settings = app_settings

    def _server_url(self) -> str:
        cfg = self.app_settings.get_all()
        return (cfg.get("ntfy_server_url") or "https://ntfy.sh").strip().rstrip("/")

    def is_configured(self) -> bool:
        return bool(self._server_url())

    def send(
        self,
        recipient: str,
        subject: str,
        body: str,
        priority: int = 5,
        tags: list[str] | None = None,
    ) -> tuple[bool, str]:
        topic = (recipient or "").strip().lstrip("/")
        if not topic:
            return False, "Kullanıcının ntfy topic alanı boş"

        payload = {
            "topic": topic,
            "title": (subject or "GameGaraj Bildirimi")[:150],
            "message": body.strip(),
            "priority": priority,
            "tags": tags or (["rotating_light"] if priority >= 5 else ["white_check_mark"]),
        }
        req = urllib.request.Request(
            self._server_url(),
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            method="POST",
            headers={"Content-Type": "application/json; charset=utf-8"},
        )
        try:
            with urllib.request.urlopen(req, timeout=15):
                return True, ""
        except Exception as exc:
            return False, str(exc)
