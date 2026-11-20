from __future__ import annotations

from typing import Protocol


class Notifier(Protocol):
    """Notification channel contract (Telegram, Email, etc.)."""

    channel: str

    def is_configured(self) -> bool: ...

    def send(self, recipient: str, subject: str, body: str) -> tuple[bool, str]: ...
