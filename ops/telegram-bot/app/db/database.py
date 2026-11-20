from __future__ import annotations

import os
import sqlite3
import threading
from contextlib import contextmanager
from datetime import datetime, timezone
from typing import Iterator

from app.config import Settings

SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    telegram_chat_id TEXT NOT NULL DEFAULT '',
    ntfy_topic TEXT NOT NULL DEFAULT '',
    color TEXT NOT NULL DEFAULT '#3b82f6',
    active INTEGER NOT NULL DEFAULT 1,
    notify_email INTEGER NOT NULL DEFAULT 1,
    notify_telegram INTEGER NOT NULL DEFAULT 1,
    notify_ntfy INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS schedule (
    day_of_week INTEGER PRIMARY KEY CHECK (day_of_week BETWEEN 0 AND 6),
    primary_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    backup_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS schedule_overrides (
    date TEXT PRIMARY KEY,
    primary_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    backup_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    note TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS incidents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    node_name TEXT NOT NULL,
    summary TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'open',
    assigned_user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    escalated INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    acked_at TEXT,
    resolved_at TEXT
);

CREATE TABLE IF NOT EXISTS alert_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    level TEXT NOT NULL,
    channel TEXT NOT NULL,
    recipient TEXT NOT NULL,
    message TEXT NOT NULL,
    success INTEGER NOT NULL,
    error TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);
"""


class Database:
    """Thread-safe SQLite wrapper with schema initialization and default seeding."""

    def __init__(self, path: str, settings: Settings) -> None:
        self.path = path
        self.settings = settings
        self._lock = threading.RLock()
        parent = os.path.dirname(path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        self._conn = sqlite3.connect(path, check_same_thread=False)
        self._conn.row_factory = sqlite3.Row
        self._init_schema()

    @contextmanager
    def session(self) -> Iterator[sqlite3.Connection]:
        with self._lock:
            try:
                yield self._conn
                self._conn.commit()
            except Exception:
                self._conn.rollback()
                raise

    def close(self) -> None:
        with self._lock:
            self._conn.close()

    def _init_schema(self) -> None:
        now = datetime.now(timezone.utc).isoformat()
        with self.session() as conn:
            conn.execute("PRAGMA foreign_keys = ON;")
            conn.execute("PRAGMA journal_mode = WAL;")
            conn.executescript(SCHEMA_SQL)

            cols = {r["name"] for r in conn.execute("PRAGMA table_info(users)").fetchall()}
            if "notify_email" not in cols:
                conn.execute("ALTER TABLE users ADD COLUMN notify_email INTEGER NOT NULL DEFAULT 1")
            if "notify_telegram" not in cols:
                conn.execute("ALTER TABLE users ADD COLUMN notify_telegram INTEGER NOT NULL DEFAULT 1")
            if "ntfy_topic" not in cols:
                conn.execute("ALTER TABLE users ADD COLUMN ntfy_topic TEXT NOT NULL DEFAULT ''")
            if "notify_ntfy" not in cols:
                conn.execute("ALTER TABLE users ADD COLUMN notify_ntfy INTEGER NOT NULL DEFAULT 1")

            for dow in range(7):
                conn.execute(
                    "INSERT OR IGNORE INTO schedule (day_of_week, primary_user_id, backup_user_id, updated_at) VALUES (?, NULL, NULL, ?)",
                    (dow, now),
                )

            defaults = {
                "report_interval_hours": str(self.settings.default_report_interval_hours),
                "night_start": self.settings.default_night_start,
                "night_end": self.settings.default_night_end,
                "mute_general_at_night": "true",
                "notify_group_on_critical": "true",
                "notify_oncall_email": "true",
                "notify_oncall_telegram": "true",
                "notify_oncall_ntfy": "true",
                "ntfy_server_url": "https://ntfy.sh",
                "escalation_minutes": "15",
                "telegram_group_enabled": "true",
                "telegram_group_chat_id": self.settings.telegram_chat_id,
                "node_alerts_enabled": "true",
                "smtp_host": self.settings.smtp_host,
                "smtp_port": str(self.settings.smtp_port),
                "smtp_user": self.settings.smtp_user,
                "smtp_pass": self.settings.smtp_pass,
                "smtp_from": self.settings.smtp_from,
                "smtp_tls": "true" if self.settings.smtp_tls else "false",
            }
            for key, value in defaults.items():
                conn.execute(
                    "INSERT OR IGNORE INTO app_settings (key, value) VALUES (?, ?)",
                    (key, value),
                )
