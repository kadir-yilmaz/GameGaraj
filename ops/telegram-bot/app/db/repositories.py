from __future__ import annotations

from datetime import datetime, timezone

from app.db.database import Database


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


class UserRepository:
    def __init__(self, db: Database) -> None:
        self.db = db

    def list_all(self) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute("SELECT * FROM users ORDER BY name ASC").fetchall()
            return [dict(r) for r in rows]

    def get(self, user_id: int) -> dict | None:
        with self.db.session() as conn:
            row = conn.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
            return dict(row) if row else None

    def create(
        self,
        name: str,
        email: str,
        telegram_chat_id: str = "",
        ntfy_topic: str = "",
        color: str = "#3b82f6",
        notify_email: bool = True,
        notify_telegram: bool = True,
        notify_ntfy: bool = True,
    ) -> dict:
        with self.db.session() as conn:
            cur = conn.execute(
                """
                INSERT INTO users (name, email, telegram_chat_id, ntfy_topic, color, active, notify_email, notify_telegram, notify_ntfy, created_at)
                VALUES (?, ?, ?, ?, ?, 1, ?, ?, ?, ?)
                """,
                (
                    name.strip(),
                    email.strip().lower(),
                    telegram_chat_id.strip(),
                    ntfy_topic.strip(),
                    color,
                    1 if notify_email else 0,
                    1 if notify_telegram else 0,
                    1 if notify_ntfy else 0,
                    utc_now(),
                ),
            )
            row = conn.execute("SELECT * FROM users WHERE id = ?", (cur.lastrowid,)).fetchone()
            return dict(row)

    def update(
        self,
        user_id: int,
        name: str,
        email: str,
        telegram_chat_id: str,
        ntfy_topic: str,
        color: str,
        active: bool,
        notify_email: bool = True,
        notify_telegram: bool = True,
        notify_ntfy: bool = True,
    ) -> dict | None:
        with self.db.session() as conn:
            conn.execute(
                """
                UPDATE users
                SET name = ?, email = ?, telegram_chat_id = ?, ntfy_topic = ?, color = ?, active = ?, notify_email = ?, notify_telegram = ?, notify_ntfy = ?
                WHERE id = ?
                """,
                (
                    name.strip(),
                    email.strip().lower(),
                    telegram_chat_id.strip(),
                    ntfy_topic.strip(),
                    color,
                    1 if active else 0,
                    1 if notify_email else 0,
                    1 if notify_telegram else 0,
                    1 if notify_ntfy else 0,
                    user_id,
                ),
            )
            row = conn.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
            return dict(row) if row else None

    def set_toggles(
        self,
        user_id: int,
        active: bool | None = None,
        notify_email: bool | None = None,
        notify_telegram: bool | None = None,
        notify_ntfy: bool | None = None,
    ) -> dict | None:
        updates, params = [], []
        if active is not None:
            updates.append("active = ?")
            params.append(1 if active else 0)
        if notify_email is not None:
            updates.append("notify_email = ?")
            params.append(1 if notify_email else 0)
        if notify_telegram is not None:
            updates.append("notify_telegram = ?")
            params.append(1 if notify_telegram else 0)
        if notify_ntfy is not None:
            updates.append("notify_ntfy = ?")
            params.append(1 if notify_ntfy else 0)
        if not updates:
            return self.get(user_id)
        params.append(user_id)
        with self.db.session() as conn:
            conn.execute(f"UPDATE users SET {', '.join(updates)} WHERE id = ?", tuple(params))
            row = conn.execute("SELECT * FROM users WHERE id = ?", (user_id,)).fetchone()
            return dict(row) if row else None

    def delete(self, user_id: int) -> bool:
        with self.db.session() as conn:
            cur = conn.execute("DELETE FROM users WHERE id = ?", (user_id,))
            return cur.rowcount > 0


class ScheduleRepository:
    def __init__(self, db: Database) -> None:
        self.db = db

    def get_weekly(self) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                """
                SELECT s.day_of_week, s.primary_user_id, s.backup_user_id, s.updated_at,
                       p.name AS primary_name, p.email AS primary_email, p.color AS primary_color,
                       b.name AS backup_name, b.email AS backup_email, b.color AS backup_color
                FROM schedule s
                LEFT JOIN users p ON p.id = s.primary_user_id
                LEFT JOIN users b ON b.id = s.backup_user_id
                ORDER BY s.day_of_week ASC
                """
            ).fetchall()
            return [dict(r) for r in rows]

    def set_day(self, day_of_week: int, primary_user_id: int | None, backup_user_id: int | None) -> dict:
        with self.db.session() as conn:
            conn.execute(
                """
                UPDATE schedule
                SET primary_user_id = ?, backup_user_id = ?, updated_at = ?
                WHERE day_of_week = ?
                """,
                (primary_user_id, backup_user_id, utc_now(), day_of_week),
            )
        return self.get_day(day_of_week)

    def get_day(self, day_of_week: int) -> dict:
        with self.db.session() as conn:
            row = conn.execute("SELECT * FROM schedule WHERE day_of_week = ?", (day_of_week,)).fetchone()
            return dict(row) if row else {}

    def swap_days(self, source_day: int, target_day: int, role: str = "primary") -> None:
        col = "primary_user_id" if role == "primary" else "backup_user_id"
        with self.db.session() as conn:
            src = conn.execute(f"SELECT {col} FROM schedule WHERE day_of_week = ?", (source_day,)).fetchone()
            dst = conn.execute(f"SELECT {col} FROM schedule WHERE day_of_week = ?", (target_day,)).fetchone()
            if not src or not dst:
                return
            now = utc_now()
            conn.execute(f"UPDATE schedule SET {col} = ?, updated_at = ? WHERE day_of_week = ?", (dst[0], now, source_day))
            conn.execute(f"UPDATE schedule SET {col} = ?, updated_at = ? WHERE day_of_week = ?", (src[0], now, target_day))

    def list_overrides(self) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                """
                SELECT o.*, p.name AS primary_name, b.name AS backup_name
                FROM schedule_overrides o
                LEFT JOIN users p ON p.id = o.primary_user_id
                LEFT JOIN users b ON b.id = o.backup_user_id
                ORDER BY o.date ASC
                """
            ).fetchall()
            return [dict(r) for r in rows]

    def get_override(self, date_str: str) -> dict | None:
        with self.db.session() as conn:
            row = conn.execute("SELECT * FROM schedule_overrides WHERE date = ?", (date_str,)).fetchone()
            return dict(row) if row else None

    def set_override(self, date_str: str, primary_user_id: int | None, backup_user_id: int | None, note: str = "") -> dict:
        with self.db.session() as conn:
            conn.execute(
                """
                INSERT INTO schedule_overrides (date, primary_user_id, backup_user_id, note)
                VALUES (?, ?, ?, ?)
                ON CONFLICT(date) DO UPDATE SET
                    primary_user_id = excluded.primary_user_id,
                    backup_user_id = excluded.backup_user_id,
                    note = excluded.note
                """,
                (date_str, primary_user_id, backup_user_id, note.strip()),
            )
            row = conn.execute("SELECT * FROM schedule_overrides WHERE date = ?", (date_str,)).fetchone()
            return dict(row)

    def delete_override(self, date_str: str) -> bool:
        with self.db.session() as conn:
            cur = conn.execute("DELETE FROM schedule_overrides WHERE date = ?", (date_str,))
            return cur.rowcount > 0


class SettingsRepository:
    def __init__(self, db: Database) -> None:
        self.db = db

    def get_all(self) -> dict[str, str]:
        with self.db.session() as conn:
            rows = conn.execute("SELECT key, value FROM app_settings").fetchall()
            return {r["key"]: r["value"] for r in rows}

    def update_many(self, values: dict[str, str]) -> dict[str, str]:
        with self.db.session() as conn:
            for key, value in values.items():
                conn.execute(
                    """
                    INSERT INTO app_settings (key, value) VALUES (?, ?)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value
                    """,
                    (key, str(value)),
                )
        return self.get_all()


class IncidentRepository:
    def __init__(self, db: Database) -> None:
        self.db = db

    def create(self, node_name: str, summary: str, assigned_user_id: int | None) -> dict:
        with self.db.session() as conn:
            cur = conn.execute(
                """
                INSERT INTO incidents (node_name, summary, status, assigned_user_id, escalated, created_at)
                VALUES (?, ?, 'open', ?, 0, ?)
                """,
                (node_name, summary, assigned_user_id, utc_now()),
            )
            row = conn.execute("SELECT * FROM incidents WHERE id = ?", (cur.lastrowid,)).fetchone()
            return dict(row)

    def list_recent(self, limit: int = 30) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                """
                SELECT i.*, u.name AS assigned_name, u.email AS assigned_email
                FROM incidents i
                LEFT JOIN users u ON u.id = i.assigned_user_id
                ORDER BY i.id DESC
                LIMIT ?
                """,
                (limit,),
            ).fetchall()
            return [dict(r) for r in rows]

    def list_active(self) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                """
                SELECT i.*, u.name AS assigned_name, u.email AS assigned_email
                FROM incidents i
                LEFT JOIN users u ON u.id = i.assigned_user_id
                WHERE i.status IN ('open', 'acked')
                ORDER BY i.id DESC
                """
            ).fetchall()
            return [dict(r) for r in rows]

    def list_open_unescalated(self) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                "SELECT * FROM incidents WHERE status = 'open' AND escalated = 0"
            ).fetchall()
            return [dict(r) for r in rows]

    def mark_escalated(self, incident_id: int) -> None:
        with self.db.session() as conn:
            conn.execute("UPDATE incidents SET escalated = 1 WHERE id = ?", (incident_id,))

    def ack(self, incident_id: int) -> dict | None:
        with self.db.session() as conn:
            conn.execute(
                "UPDATE incidents SET status = 'acked', acked_at = ? WHERE id = ? AND status = 'open'",
                (utc_now(), incident_id),
            )
            row = conn.execute("SELECT * FROM incidents WHERE id = ?", (incident_id,)).fetchone()
            return dict(row) if row else None

    def ack_all_open(self) -> int:
        with self.db.session() as conn:
            cur = conn.execute(
                "UPDATE incidents SET status = 'acked', acked_at = ? WHERE status = 'open'",
                (utc_now(),),
            )
            return cur.rowcount

    def resolve_by_node(self, node_name: str) -> int:
        with self.db.session() as conn:
            cur = conn.execute(
                "UPDATE incidents SET status = 'resolved', resolved_at = ? WHERE node_name = ? AND status IN ('open', 'acked')",
                (utc_now(), node_name),
            )
            return cur.rowcount


class AlertLogRepository:
    def __init__(self, db: Database) -> None:
        self.db = db

    def add(self, level: str, channel: str, recipient: str, message: str, success: bool, error: str = "") -> None:
        with self.db.session() as conn:
            conn.execute(
                """
                INSERT INTO alert_logs (level, channel, recipient, message, success, error, created_at)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (level, channel, recipient, message[:1000], 1 if success else 0, error[:500], utc_now()),
            )

    def list_recent(self, limit: int = 50) -> list[dict]:
        with self.db.session() as conn:
            rows = conn.execute(
                "SELECT * FROM alert_logs ORDER BY id DESC LIMIT ?",
                (limit,),
            ).fetchall()
            return [dict(r) for r in rows]
