from __future__ import annotations

import json
import logging
import time
import urllib.parse
import urllib.request

from app.k8s.reports import chunk_lines

log = logging.getLogger(__name__)


class TelegramNotifier:
    channel = "telegram"

    def __init__(self, token: str) -> None:
        self.token = token
        self.base_url = f"https://api.telegram.org/bot{token}" if token else ""

    def is_configured(self) -> bool:
        return bool(self.token)

    def _post_json(self, method: str, data: dict, timeout: int = 30) -> dict:
        if not self.is_configured():
            raise RuntimeError("Telegram bot token ayarlı değil")
        body = urllib.parse.urlencode(data).encode()
        req = urllib.request.Request(
            f"{self.base_url}/{method}",
            data=body,
            headers={"Content-Type": "application/x-www-form-urlencoded"},
        )
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return json.load(resp)

    def send(self, recipient: str, subject: str, body: str) -> tuple[bool, str]:
        if not self.is_configured():
            return False, "Telegram token yok"
        if not recipient:
            return False, "Telegram chat_id boş"
        text = f"{subject}\n\n{body}".strip() if subject else body
        last_err = ""
        for chunk in chunk_lines(text.split("\n")):
            sent = False
            for attempt in range(3):
                try:
                    self._post_json(
                        "sendMessage",
                        {"chat_id": recipient, "text": chunk, "disable_web_page_preview": "true"},
                        timeout=30,
                    )
                    sent = True
                    break
                except Exception as exc:
                    last_err = str(exc)
                    log.warning("telegram send failed (attempt %d): %s", attempt + 1, exc)
                    time.sleep(1.5 * (attempt + 1))
            if not sent:
                return False, last_err
        return True, ""

    def send_parts(self, chat_id: str, parts: list[str]) -> None:
        for part in parts:
            self.send(chat_id, "", part)

    def drain_pending(self) -> int:
        payload = self._post_json("getUpdates", {"timeout": "0"}, timeout=20)
        updates = payload.get("result") or []
        return int(updates[-1]["update_id"]) + 1 if updates else 0

    def get_updates(self, offset: int) -> dict:
        return self._post_json(
            "getUpdates",
            {"timeout": "50", "offset": str(offset), "allowed_updates": json.dumps(["message"])},
            timeout=70,
        )
